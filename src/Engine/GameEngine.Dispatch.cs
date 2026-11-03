using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed partial class GameEngine
{
	/// <summary>
	/// Single place where Spend costs for player actions are paid. Events must not re-spend cards.
	/// </summary>
	private void PayActionCosts(IPlayerAction action, AbilityDefinition? ability = null, int? performerId = null, CardDefinition? sourceDef = null)
	{
		int? spendId = null;
		switch (action)
		{
		case MarchAction march:
			spendId = march.CardInstanceId;
			break;
		case AttackAction attack:
			spendId = attack.AttackerInstanceId;
			break;
		case UseAbilityAction use:
			if (ability != null && ability.RequiresSpend)
			{
				spendId = use.SourceInstanceId;
			}
			break;
		case PlayCardAction:
			if (ability != null && performerId.HasValue && (ability.RequiresSpend || (sourceDef != null && Regex.IsMatch(sourceDef.RulesText, "\\bSpend\\s+(?:Limited\\s+)?(?:Order|React):", RegexOptions.IgnoreCase))))
			{
				spendId = performerId;
			}
			break;
		}
		if (spendId.HasValue)
		{
			CardLocator.FindAndMutate(_state, spendId.Value, delegate(ref CardInstance c)
			{
				c.ReadyState = ReadyState.Spent;
			});
		}
	}

	private void PushEventsForAction(IPlayerAction action)
	{
		switch (action)
		{
		case SelectStrikeTargetAction selectStrikeTargetAction:
		{
			StrikeTargetPrompt? prompt = _state.PendingPrompt;
			_state.PendingPrompt = null;
			if (prompt != null)
			{
				if (selectStrikeTargetAction.TargetInstanceId.HasValue)
				{
					_resolver.Push(new DeclareAttackEvent
					{
						AttackerInstanceId = prompt.Attacker.InstanceId,
						DefenderInstanceId = selectStrikeTargetAction.TargetInstanceId.Value,
						StartStrikeIndex = prompt.StrikeIndex
					});
				}
				else
				{
					_resolver.Push(new DeclareAttackEvent
					{
						AttackerInstanceId = prompt.Attacker.InstanceId,
						DefenderInstanceId = prompt.PreviousTargetId ?? prompt.Attacker.InstanceId,
						StartStrikeIndex = prompt.TotalStrikes,
						SkipRemainingStrikes = true
					});
				}
			}
			return;
		}
		case DiscardCardAction discardCardAction:
		{
			_resolver.Push(new DiscardCardEvent
			{
				PlayerIndex = discardCardAction.PlayerIndex,
				CardInstanceId = discardCardAction.CardInstanceId
			});
			return;
		}
		case MarchAction marchAction:
		{
			PayActionCosts(action);
			_resolver.Push(new MarchEvent
			{
				PlayerIndex = marchAction.PlayerIndex,
				CardInstanceId = marchAction.CardInstanceId,
				SourceRank = marchAction.SourceRank,
				DestinationRank = marchAction.DestinationRank,
				TargetSlotIndex = marchAction.TargetSlotIndex
			});
			return;
		}
		case AttackAction attackAction:
		{
			PayActionCosts(action);
			_resolver.Push(new DeclareAttackEvent
			{
				AttackerInstanceId = attackAction.AttackerInstanceId,
				DefenderInstanceId = attackAction.TargetInstanceId,
				DefenderInstanceIds = attackAction.TargetInstanceIds
			});
			return;
		}
		case DeployStartingArmyAction deployStartingArmyAction:
		{
			PlayerState deployingPlayer = _state.Players[deployStartingArmyAction.PlayerIndex];
			foreach (StartingCharacterDeployment dep in deployStartingArmyAction.Deployments)
			{
				CardInstance? foundCard = null;
				int handIndex = deployingPlayer.Hand.FindIndex((CardInstance c) => c.InstanceId == dep.CardInstanceId);
				if (handIndex >= 0)
				{
					foundCard = deployingPlayer.Hand[handIndex];
					deployingPlayer.Hand.RemoveAt(handIndex);
				}
				else
				{
					int deckIndex = deployingPlayer.Deck.FindIndex((CardInstance c) => c.InstanceId == dep.CardInstanceId);
					if (deckIndex >= 0)
					{
						foundCard = deployingPlayer.Deck[deckIndex];
						deployingPlayer.Deck.RemoveAt(deckIndex);
					}
				}
				if (foundCard.HasValue)
				{
					CardInstance deployedCard = foundCard.Value with
					{
						Zone = CardZone.Board,
						Rank = dep.TargetRank
					};
					if (!deployingPlayer.Ranks.TryGetValue(dep.TargetRank, out List<CardInstance> targetRankCards))
					{
						targetRankCards = new List<CardInstance>();
						deployingPlayer.Ranks[dep.TargetRank] = targetRankCards;
					}
					targetRankCards.Add(deployedCard);
					TriggerCharacterEnterReacts(deployedCard, dep.TargetRank, deployStartingArmyAction.PlayerIndex);
					CardDefinition definition3 = _cardDb.GetDefinition(deployedCard.DefinitionId);
					if (definition3 != null && definition3.IsWarlord)
					{
						deployingPlayer.WarlordInstanceId = deployedCard.InstanceId;
					}
				}
			}
			if (_state.CurrentPhase == TurnPhase.Setup && _state.Players.All((PlayerState p) => p.WarlordInstanceId.HasValue))
			{
				_state.CurrentPhase = TurnPhase.Ready;
				_state.TurnNumber = 1;
				UnspendAndUnstunCards();
			}
			return;
		}
		case PassAction passAction:
		{
			passAction.Execute(_state);
			return;
		}
		case UseAbilityAction useAbilityAction:
		{
										CardInstance? cardInstance = null;
										foreach (PlayerState player in _state.Players)
										{
											foreach (List<CardInstance> rankCards in player.Ranks.Values)
											{
												foreach (CardInstance item6 in rankCards)
												{
													if (item6.InstanceId == useAbilityAction.SourceInstanceId)
													{
														cardInstance = item6;
														break;
													}
												}
												if (cardInstance.HasValue)
												{
													break;
												}
											}
											if (cardInstance.HasValue)
											{
												break;
											}
										}
										if (!cardInstance.HasValue)
										{
											cardInstance = CardLocator.FindCard(_state, useAbilityAction.SourceInstanceId);
										}
										if (!cardInstance.HasValue)
										{
											return;
										}
										CardDefinition definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
										int abilityIdx = useAbilityAction.AbilityIndex;
										if (useAbilityAction.ChoiceIndex.HasValue && useAbilityAction.ChoiceIndex.Value >= 0 && definition != null && useAbilityAction.ChoiceIndex.Value < definition.Abilities.Count)
										{
											abilityIdx = useAbilityAction.ChoiceIndex.Value;
										}
										AbilityDefinition abilityDefinition = definition.Abilities[abilityIdx];
										if (abilityDefinition.IsLimited)
										{
											string item = $"{useAbilityAction.SourceInstanceId}_{abilityIdx}";
											_state.TurnState.UsedLimitedAbilityKeysThisTurn.Add(item);
											_state.TurnState.UsedLimitedAbilityKeysThisTurn.Add($"{useAbilityAction.SourceInstanceId}_Limited");
											CardLocator.FindAndMutate(_state, useAbilityAction.SourceInstanceId, delegate(ref CardInstance c)
											{
												if (!c.AbilitiesUsedThisTurn.Contains(abilityIdx))
												{
													c.AbilitiesUsedThisTurn.Add(abilityIdx);
												}
											});
										}
										if (abilityDefinition.DiscardHandCost > 0)
										{
											PlayerState playerState = _state.Players[useAbilityAction.PlayerIndex];
											int discardId = useAbilityAction.DiscardCardInstanceId ?? ((playerState.Hand.Count > 0) ? playerState.Hand[0].InstanceId : (-1));
											int discardIndex = playerState.Hand.FindIndex((CardInstance c) => c.InstanceId == discardId);
											if (discardIndex >= 0)
											{
												CardInstance discardedCard = playerState.Hand[discardIndex];
												playerState.Hand.RemoveAt(discardIndex);
												playerState.DiscardPile.Add(discardedCard with
												{
													Zone = CardZone.Discard
												});
											}
										}
										int performerId = ResolvePerformerCharacterId(_state, useAbilityAction.SourceInstanceId, _cardDb);
										if (abilityDefinition.RequiresSpendAdjacentAlly || abilityDefinition.RequiresSpendAllyInRank)
										{
											int? costCharacterId = useAbilityAction.CostCharacterInstanceId;
											(int, int, int)? tuple = FindCharacterPosition(_state, performerId);
											if (tuple.HasValue)
											{
												List<CardInstance> list = _state.Players[tuple.Value.Item1].Ranks[tuple.Value.Item2];
												if (!costCharacterId.HasValue)
												{
													foreach (CardInstance item7 in list)
													{
														if (item7.InstanceId != performerId && item7.ReadyState == ReadyState.Ready && (!abilityDefinition.RequiresSpendAdjacentAlly || Math.Abs(list.IndexOf(item7) - tuple.Value.Item3) == 1))
														{
															costCharacterId = item7.InstanceId;
															break;
														}
													}
												}
											}
											if (costCharacterId.HasValue)
											{
												CardLocator.FindAndMutate(_state, costCharacterId.Value, delegate(ref CardInstance c)
												{
													c.ReadyState = ReadyState.Spent;
												});
											}
										}
										if (abilityDefinition.BanishActionFromDiscardCost > 0)
										{
											PlayerState actingPlayer = _state.Players[useAbilityAction.PlayerIndex];
											foreach (CardInstance ac in actingPlayer.DiscardPile.Where((CardInstance c) =>
											{
												CardDefinition? definition7 = _cardDb.GetDefinition(c.DefinitionId);
												return (object)definition7 != null && definition7.Type == CardType.Action;
											}).Take(abilityDefinition.BanishActionFromDiscardCost).ToList())
											{
												actingPlayer.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == ac.InstanceId);
												actingPlayer.BanishPile.Add(ac with
												{
													Zone = CardZone.Banished
												});
											}
										}
										if (abilityDefinition.DiscardsOpponentTopDeck)
										{
											PlayerState opponentState = _state.Players.FirstOrDefault((PlayerState pl) => pl.PlayerIndex != useAbilityAction.PlayerIndex);
											int strikeBonus = 0;
											if (opponentState != null && opponentState.Deck.Count > 0)
											{
												CardInstance milledCard = opponentState.Deck[0];
												opponentState.Deck.RemoveAt(0);
												opponentState.DiscardPile.Add(milledCard with
												{
													Zone = CardZone.Discard
												});
												strikeBonus = _cardDb.GetDefinition(milledCard.DefinitionId)?.Level ?? 0;
											}
											abilityDefinition = abilityDefinition with
											{
												Effects = [new PerformStrikeEffect(strikeBonus, IsRanged: true)]
											};
										}
										if (abilityDefinition.StunSelfCost)
										{
											CardLocator.FindAndMutate(_state, useAbilityAction.SourceInstanceId, delegate(ref CardInstance c)
											{
												c.ReadyState = ReadyState.Stunned;
											});
										}
										if (!CardScriptRegistry.PreservesSourceOnUse(definition) && (abilityDefinition.DestroysSource || Regex.IsMatch(abilityDefinition.Description ?? "", "\\b(?:Destroy|Discard|Sacrifice)\\s+(?:this\\s+item|this\\s+card)\\b", RegexOptions.IgnoreCase) || (definition != null && definition.Type == CardType.Item && (Regex.IsMatch(definition.RulesText, "\\b(?:Destroy|Discard)\\s+(?:this\\s+item|Order:)\\b", RegexOptions.IgnoreCase) || CardScriptRegistry.DestroysSourceOnUse(definition)))))
										{
											foreach (PlayerState player2 in _state.Players)
											{
												foreach (List<CardInstance> rankCards in player2.Ranks.Values)
												{
													for (int itemHolderIdx = 0; itemHolderIdx < rankCards.Count; itemHolderIdx++)
													{
														if (rankCards[itemHolderIdx].AttachedItemIds.Contains(useAbilityAction.SourceInstanceId))
														{
															List<int> list2 = new List<int>(rankCards[itemHolderIdx].AttachedItemIds);
															list2.Remove(useAbilityAction.SourceInstanceId);
															rankCards[itemHolderIdx] = rankCards[itemHolderIdx] with
															{
																AttachedItemIds = list2
															};
														}
													}
												}
											}
											CardLocator.FindAndMutate(_state, useAbilityAction.SourceInstanceId, delegate(ref CardInstance c)
											{
												c.Zone = CardZone.Discard;
												c.AttachedToInstanceId = null;
											});
											PlayerState itemOwnerState = _state.Players[cardInstance.Value.OwnerPlayerIndex];
											foreach (PlayerState p in _state.Players)
											{
												p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == useAbilityAction.SourceInstanceId);
											}
											if (!itemOwnerState.DiscardPile.Any((CardInstance c) => c.InstanceId == useAbilityAction.SourceInstanceId))
											{
												itemOwnerState.DiscardPile.Add(cardInstance.Value with
												{
													Zone = CardZone.Discard,
													AttachedToInstanceId = null
												});
											}
										}
										PayActionCosts(action, abilityDefinition);
										if (definition != null)
										{
											CardScriptContext abilityCtx = new CardScriptContext
											{
												State = _state,
												CardDb = _cardDb,
												Card = cardInstance.Value,
												Definition = definition,
												PerformerId = performerId,
												PlayerIndex = action.PlayerIndex
											};
											CardScriptRegistry.OnAbilityUsed(abilityCtx);
										}
										_resolver.Push(new AbilityActivatedEvent
										{
											SourceInstanceId = useAbilityAction.SourceInstanceId,
											PerformerInstanceId = performerId,
											AbilityIndex = abilityIdx,
											TargetInstanceIds = useAbilityAction.TargetInstanceIds,
											Ability = abilityDefinition,
											OwnerPlayerIndex = cardInstance.Value.OwnerPlayerIndex
										});
			return;
		}
		case PlayCardAction playCardAction:
		{
		PlayerState actingPlayer = _state.Players[playCardAction.PlayerIndex];
		int cardHandIndex = actingPlayer.Hand.FindIndex((CardInstance c) => c.InstanceId == playCardAction.CardInstanceId);
		if (cardHandIndex < 0)
		{
			return;
		}
		CardInstance card = actingPlayer.Hand[cardHandIndex];
		CardDefinition cardDef = _cardDb.GetDefinition(card.DefinitionId);
		CardType playedType = cardDef?.Type ?? CardType.Character;
		if (playedType != CardType.Character && playedType != CardType.Token)
		{
			actingPlayer.Hand.RemoveAt(cardHandIndex);
		}
		switch (cardDef?.Type ?? CardType.Character)
		{
		case CardType.Character:
		case CardType.Token:
		{
			_resolver.Push(new PlayCardEvent
			{
				PlayerIndex = playCardAction.PlayerIndex,
				CardInstanceId = card.InstanceId,
				TargetRank = playCardAction.TargetRank ?? 1,
				CardDb = _cardDb,
				OnEntered = TriggerCharacterEnterReacts
			});
			break;
		}
		case CardType.Item:
		{
			int? targetCharId = playCardAction.TargetCharacterId ?? playCardAction.TargetInstanceId;
			if (targetCharId.HasValue)
			{
				CardInstance attachedCard = card with
				{
					Zone = CardZone.Attached,
					AttachedToInstanceId = targetCharId.Value
				};
				(int, int, int) itemPassiveStats = GetItemPassiveStats(cardDef);
				foreach (PlayerState player3 in _state.Players)
				{
					foreach (List<CardInstance> targetRankCards in player3.Ranks.Values)
					{
						for (int targetCharIdx = 0; targetCharIdx < targetRankCards.Count; targetCharIdx++)
						{
							if (targetRankCards[targetCharIdx].InstanceId == targetCharId.Value)
							{
								CardInstance targetChar = targetRankCards[targetCharIdx];
								List<int> attachedItemIds = new List<int>(targetChar.AttachedItemIds) { card.InstanceId };
								targetRankCards[targetCharIdx] = targetChar with
								{
									AttachedItemIds = attachedItemIds,
									CurrentArmorClass = targetChar.CurrentArmorClass + itemPassiveStats.Item2,
									CurrentHitPoints = targetChar.CurrentHitPoints + itemPassiveStats.Item3
								};
								break;
							}
						}
					}
				}
				if (!actingPlayer.AttachedItems.Any((CardInstance c) => c.InstanceId == attachedCard.InstanceId))
				{
					actingPlayer.AttachedItems.Add(attachedCard);
				}
			}
			else
			{
				CardInstance discardedItem = card with
				{
					Zone = CardZone.Discard
				};
				if (!actingPlayer.DiscardPile.Any((CardInstance c) => c.InstanceId == discardedItem.InstanceId))
				{
					actingPlayer.DiscardPile.Add(discardedItem);
				}
			}
			break;
		}
		case CardType.Action:
		{
			CardInstance item3 = card with
			{
				Zone = CardZone.Discard
			};
			actingPlayer.DiscardPile.Add(item3);
			int performerId = playCardAction.PerformerInstanceId ?? FindDefaultPerformer(actingPlayer, cardDef);
			if (!(cardDef != null) || cardDef.Abilities.Count <= 0)
			{
				break;
			}
			int abilityIdx = playCardAction.AbilityIndex ?? playCardAction.ChoiceIndex ?? ((_state.Priority.IsReactWindowOpen && cardDef.Abilities.Any((AbilityDefinition a) => a.IsReact)) ? cardDef.Abilities.Select((AbilityDefinition a, int idx) => (a: a, idx: idx)).FirstOrDefault(((AbilityDefinition a, int idx) x) => x.a.IsReact).idx : 0);
			if (abilityIdx < 0 || abilityIdx >= cardDef.Abilities.Count)
			{
				abilityIdx = 0;
			}
			AbilityDefinition chosenAbility = cardDef.Abilities[abilityIdx];
			PayActionCosts(action, chosenAbility, performerId, cardDef);
			int[] targetInstanceIds = ((!playCardAction.TargetCharacterId.HasValue) ? Array.Empty<int>() : new int[1] { playCardAction.TargetCharacterId.Value });
			CardInstance? performerCard = CardLocator.FindCard(_state, performerId);
			int performerLevel = 0;
			if (performerCard.HasValue)
			{
				performerLevel = GetEffectiveLevel(_state, performerCard.Value, _cardDb);
			}
			if (cardDef != null)
			{
				CardScriptContext actionCtx = new CardScriptContext
				{
					State = _state,
					CardDb = _cardDb,
					Card = card,
					Definition = cardDef,
					PerformerId = performerId,
					PlayerIndex = playCardAction.PlayerIndex,
					TargetInstanceId = playCardAction.TargetCharacterId,
					PerformerLevel = performerLevel,
					ActingPlayer = actingPlayer
				};
				CardScriptRegistry.OnActionPlayed(actionCtx);
			}
			_resolver.Push(new AbilityActivatedEvent
			{
				SourceInstanceId = performerId,
				AbilityIndex = abilityIdx,
				TargetInstanceIds = targetInstanceIds,
				Ability = chosenAbility,
				OwnerPlayerIndex = playCardAction.PlayerIndex
			});
			break;
		}
		}
		break;
		}
		}
	}

	public CardInstance CreateCardInstance(CardDefinition definition, int ownerPlayerIndex, CardZone initialZone)
	{
		int instanceId = _state.NextInstanceId++;
		return new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = definition.DefinitionId,
			CurrentHitPoints = definition.BaseHitPoints.GetValueOrDefault(),
			CurrentArmorClass = definition.ArmorClass.GetValueOrDefault(),
			CurrentSkill = definition.Skill.GetValueOrDefault(),
			ReadyState = ReadyState.Ready,
			Zone = initialZone,
			OwnerPlayerIndex = ownerPlayerIndex,
			SkillRatings = new Dictionary<string, int>(definition.SkillRatings, StringComparer.OrdinalIgnoreCase)
		};
	}

	private void DrainResolverStack(int? actingPlayerIndex = null)
	{
		while (_resolver.PendingCount > 0)
		{
			GameEvent gameEvent = _resolver.Peek()!;
			if (gameEvent is CharacterDefeatedEvent cde && cde.BanishInsteadOfDiscard)
			{
				_resolver.Resolve(_state);
				continue;
			}
			if (gameEvent is DeclareAttackEvent resumedAttack && (resumedAttack.StartStrikeIndex > 0 || resumedAttack.SkipRemainingStrikes))
			{
				_resolver.Resolve(_state);
				continue;
			}
			if ((!EventResolver.RequiresReactWindow(gameEvent) && !(gameEvent is CharacterDefeatedEvent)) || (gameEvent is AbilityActivatedEvent abilityActivatedEvent && abilityActivatedEvent.Ability.SplitSecond))
			{
				_resolver.Resolve(_state);
				continue;
			}
			bool shouldOpenReactWindow = (gameEvent is not CharacterDefeatedEvent && gameEvent is not PlayCardEvent && !_state.AutoPassWhenNoReactsAvailable)
				|| HasAnyLegalReact(_state);
			if (!shouldOpenReactWindow)
			{
				_resolver.Resolve(_state);
				continue;
			}
			_state.Priority.IsReactWindowOpen = true;
			_state.Priority.ConsecutivePasses = 0;
			_state.Priority.HasPassed = false;
			if (_state.Players.Count > 0)
			{
				int actingPlayer = actingPlayerIndex ?? _state.ActivePlayerIndex;
				int initPriority = _state.InitiativeWinnerIndex ?? ((actingPlayer + 1) % _state.Players.Count);
				_state.Priority.ActivePlayerIndex = initPriority % _state.Players.Count;
			}
			SyncPendingEvents();
			return;
		}
		CloseReactWindow();
		FinalizePendingPrompt();
		SyncPendingEvents();
	}

	private void ResolveReactWindowStack()
	{
		while (_resolver.PendingCount > 0)
		{
			_resolver.Resolve(_state);
		}
		CloseReactWindow();
		FinalizePendingPrompt();
		SyncPendingEvents();
		if (_state.PendingPrompt == null && _state.CurrentPhase == TurnPhase.Order)
		{
			_state.TurnState.RecordAction();
		}
	}

	private void FinalizePendingPrompt()
	{
		if (_state.PendingPrompt == null)
		{
			return;
		}
		if (_state.IsGameOver)
		{
			_state.PendingPrompt = null;
			return;
		}
		StrikeTargetPrompt prompt = _state.PendingPrompt;
		CardInstance? currentAttacker = CardLocator.FindCard(_state, prompt.Attacker.InstanceId);
		if (!currentAttacker.HasValue || currentAttacker.Value.Zone != CardZone.Board || currentAttacker.Value.ReadyState == ReadyState.Stunned || currentAttacker.Value.CurrentHitPoints <= 0 || currentAttacker.Value.Wounds >= GetEffectiveMaxHitPoints(_state, currentAttacker.Value, _cardDb))
		{
			_state.PendingPrompt = null;
			return;
		}
		List<CardInstance> legalTargets = AttackResolutionHandler.GetLegalDefendersForStrike(_state, currentAttacker.Value, prompt.Strike, _cardDb);
		if (legalTargets.Count == 0)
		{
			_state.PendingPrompt = null;
			_resolver.Push(new DeclareAttackEvent
			{
				AttackerInstanceId = prompt.Attacker.InstanceId,
				DefenderInstanceId = prompt.PreviousTargetId ?? prompt.Attacker.InstanceId,
				StartStrikeIndex = prompt.TotalStrikes,
				SkipRemainingStrikes = true
			});
			while (_resolver.PendingCount > 0)
			{
				_resolver.Resolve(_state);
			}
			return;
		}
		_state.PendingPrompt = prompt with
		{
			State = _state,
			Attacker = currentAttacker.Value,
			LegalTargets = legalTargets
		};
	}

	private void CloseReactWindow()
	{
		if (_state.Priority.IsReactWindowOpen)
		{
			_state.Priority.IsReactWindowOpen = false;
			_state.Priority.ConsecutivePasses = 0;
			_state.Priority.HasPassed = false;
			_state.Priority.ActivePlayerIndex = _state.ActivePlayerIndex;
		}
	}

	private void SyncPendingEvents()
	{
		_state.PendingEvents.Clear();
		foreach (GameEvent pendingEvent in _resolver.PendingEvents)
		{
			_state.PendingEvents.Add(pendingEvent);
		}
	}

	private int FindDefaultPerformer(PlayerState player, CardDefinition? actionDef)
	{
		if (actionDef != null)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item in value)
				{
					CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
					if (definition != null && PlayCardAction.ValidatePerformerRequirements(actionDef, definition, item).IsSuccess)
					{
						return item.InstanceId;
					}
				}
			}
		}
		if (player.WarlordInstanceId.HasValue)
		{
			return player.WarlordInstanceId.Value;
		}
		foreach (List<CardInstance> rankCards in player.Ranks.Values)
		{
			if (rankCards.Count > 0)
			{
				return rankCards[0].InstanceId;
			}
		}
		return 0;
	}

}
