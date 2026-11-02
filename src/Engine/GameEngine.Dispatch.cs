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
	private void PushEventsForAction(IPlayerAction action)
	{
		if (action is DiscardCardAction discardCardAction)
		{
			DiscardCardFromHand(discardCardAction.PlayerIndex, discardCardAction.CardInstanceId);
			return;
		}
		if (action is MarchAction marchAction)
		{
			CardLocator.FindAndMutate(_state, marchAction.CardInstanceId, delegate(ref CardInstance reference)
			{
				reference.ReadyState = ReadyState.Spent;
			});
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
		if (action is AttackAction attackAction)
		{
			CardLocator.FindAndMutate(_state, attackAction.AttackerInstanceId, delegate(ref CardInstance reference)
			{
				reference.ReadyState = ReadyState.Spent;
			});
			_resolver.Push(new DeclareAttackEvent
			{
				AttackerInstanceId = attackAction.AttackerInstanceId,
				DefenderInstanceId = attackAction.TargetInstanceId,
				DefenderInstanceIds = attackAction.TargetInstanceIds
			});
			return;
		}
		if (action is DeployStartingArmyAction deployStartingArmyAction)
		{
			PlayerState playerState5 = _state.Players[deployStartingArmyAction.PlayerIndex];
			foreach (StartingCharacterDeployment dep in deployStartingArmyAction.Deployments)
			{
				CardInstance? cardInstance4 = null;
				int num4 = playerState5.Hand.FindIndex((CardInstance c) => c.InstanceId == dep.CardInstanceId);
				if (num4 >= 0)
				{
					cardInstance4 = playerState5.Hand[num4];
					playerState5.Hand.RemoveAt(num4);
				}
				else
				{
					int num5 = playerState5.Deck.FindIndex((CardInstance c) => c.InstanceId == dep.CardInstanceId);
					if (num5 >= 0)
					{
						cardInstance4 = playerState5.Deck[num5];
						playerState5.Deck.RemoveAt(num5);
					}
				}
				if (cardInstance4.HasValue)
				{
					CardInstance cardInstance5 = cardInstance4.Value with
					{
						Zone = CardZone.Board,
						Rank = dep.TargetRank
					};
					if (!playerState5.Ranks.TryGetValue(dep.TargetRank, out List<CardInstance> value2))
					{
						value2 = new List<CardInstance>();
						playerState5.Ranks[dep.TargetRank] = value2;
					}
					value2.Add(cardInstance5);
					TriggerCharacterEnterReacts(cardInstance5, dep.TargetRank, deployStartingArmyAction.PlayerIndex);
					CardDefinition definition3 = _cardDb.GetDefinition(cardInstance5.DefinitionId);
					if (definition3 != null && definition3.IsWarlord)
					{
						playerState5.WarlordInstanceId = cardInstance5.InstanceId;
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
		if (action is PassAction passAction)
		{
			passAction.Execute(_state);
			return;
		}
		if (action is UseAbilityAction useAbilityAction)
		{
										CardInstance? cardInstance = null;
										foreach (PlayerState player in _state.Players)
										{
											foreach (List<CardInstance> value5 in player.Ranks.Values)
											{
												foreach (CardInstance item6 in value5)
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
											int num = playerState.Hand.FindIndex((CardInstance c) => c.InstanceId == discardId);
											if (num >= 0)
											{
												CardInstance cardInstance2 = playerState.Hand[num];
												playerState.Hand.RemoveAt(num);
												playerState.DiscardPile.Add(cardInstance2 with
												{
													Zone = CardZone.Discard
												});
											}
										}
										int performerId = ResolvePerformerCharacterId(_state, useAbilityAction.SourceInstanceId, _cardDb);
										if (abilityDefinition.RequiresSpendAdjacentAlly || abilityDefinition.RequiresSpendAllyInRank)
										{
											int? num2 = useAbilityAction.CostCharacterInstanceId;
											(int, int, int)? tuple = FindCharacterPosition(_state, performerId);
											if (tuple.HasValue)
											{
												List<CardInstance> list = _state.Players[tuple.Value.Item1].Ranks[tuple.Value.Item2];
												if (!num2.HasValue)
												{
													foreach (CardInstance item7 in list)
													{
														if (item7.InstanceId != performerId && item7.ReadyState == ReadyState.Ready && (!abilityDefinition.RequiresSpendAdjacentAlly || Math.Abs(list.IndexOf(item7) - tuple.Value.Item3) == 1))
														{
															num2 = item7.InstanceId;
															break;
														}
													}
												}
											}
											if (num2.HasValue)
											{
												CardLocator.FindAndMutate(_state, num2.Value, delegate(ref CardInstance c)
												{
													c.ReadyState = ReadyState.Spent;
												});
											}
										}
										if (abilityDefinition.BanishActionFromDiscardCost > 0)
										{
											PlayerState playerState2 = _state.Players[useAbilityAction.PlayerIndex];
											foreach (CardInstance ac in playerState2.DiscardPile.Where((CardInstance c) =>
											{
												CardDefinition? definition7 = _cardDb.GetDefinition(c.DefinitionId);
												return (object)definition7 != null && definition7.Type == CardType.Action;
											}).Take(abilityDefinition.BanishActionFromDiscardCost).ToList())
											{
												playerState2.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == ac.InstanceId);
												playerState2.BanishPile.Add(ac with
												{
													Zone = CardZone.Banished
												});
											}
										}
										if (abilityDefinition.DiscardsOpponentTopDeck)
										{
											PlayerState playerState3 = _state.Players.FirstOrDefault((PlayerState pl) => pl.PlayerIndex != useAbilityAction.PlayerIndex);
											int strikeBonus = 0;
											if (playerState3 != null && playerState3.Deck.Count > 0)
											{
												CardInstance cardInstance3 = playerState3.Deck[0];
												playerState3.Deck.RemoveAt(0);
												playerState3.DiscardPile.Add(cardInstance3 with
												{
													Zone = CardZone.Discard
												});
												strikeBonus = _cardDb.GetDefinition(cardInstance3.DefinitionId)?.Level ?? 0;
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
										if (!definition.Name.Equals("Hod's Legacy", StringComparison.OrdinalIgnoreCase) && !definition.Name.Equals("Hunting Staff", StringComparison.OrdinalIgnoreCase) && (abilityDefinition.DestroysSource || Regex.IsMatch(abilityDefinition.Description ?? "", "\\b(?:Destroy|Discard|Sacrifice)\\s+(?:this\\s+item|this\\s+card)\\b", RegexOptions.IgnoreCase) || (definition != null && definition.Type == CardType.Item && (Regex.IsMatch(definition.RulesText, "\\b(?:Destroy|Discard)\\s+(?:this\\s+item|Order:)\\b", RegexOptions.IgnoreCase) || definition.Name.Equals("Strongarm Stout", StringComparison.OrdinalIgnoreCase)))))
										{
											foreach (PlayerState player2 in _state.Players)
											{
												foreach (List<CardInstance> value6 in player2.Ranks.Values)
												{
													for (int num3 = 0; num3 < value6.Count; num3++)
													{
														if (value6[num3].AttachedItemIds.Contains(useAbilityAction.SourceInstanceId))
														{
															List<int> list2 = new List<int>(value6[num3].AttachedItemIds);
															list2.Remove(useAbilityAction.SourceInstanceId);
															value6[num3] = value6[num3]with
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
											PlayerState playerState4 = _state.Players[cardInstance.Value.OwnerPlayerIndex];
											foreach (PlayerState p in _state.Players)
											{
												p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == useAbilityAction.SourceInstanceId);
											}
											if (!playerState4.DiscardPile.Any((CardInstance c) => c.InstanceId == useAbilityAction.SourceInstanceId))
											{
												playerState4.DiscardPile.Add(cardInstance.Value with
												{
													Zone = CardZone.Discard,
													AttachedToInstanceId = null
												});
											}
										}
										if (abilityDefinition.RequiresSpend)
										{
											CardLocator.FindAndMutate(_state, useAbilityAction.SourceInstanceId, delegate(ref CardInstance c)
											{
												c.ReadyState = ReadyState.Spent;
											});
										}
										if (definition != null)
										{
											if (definition.Name.Equals("Ciane", StringComparison.OrdinalIgnoreCase))
											{
												AddTurnBuff(_state, useAbilityAction.SourceInstanceId, "CIANE_SELF_WOUND", 1);
											}
											else if (definition.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase))
											{
												(int, int, int)? tuple2 = FindCharacterPosition(_state, performerId);
												if (tuple2.HasValue)
												{
													int item2 = tuple2.Value.Item1;
													if (_state.Players[item2].Ranks.TryGetValue(1, out List<CardInstance> value))
													{
														foreach (CardInstance item8 in value)
														{
															CardDefinition? definition2 = _cardDb.GetDefinition(item8.DefinitionId);
															int effectiveLevel = GetEffectiveLevel(_state, item8, _cardDb);
															if (IsFreeKingdoms(definition2) && effectiveLevel <= 2)
															{
																AddTurnBuff(_state, item8.InstanceId, "ATK", 2);
																AddTurnBuff(_state, item8.InstanceId, "AC", 2);
															}
														}
													}
												}
											}

											else if (definition.Name.Equals("Albrecht Novice", StringComparison.OrdinalIgnoreCase))
											{
												(int, int, int)? tuple4 = FindCharacterPosition(_state, performerId);
												if (tuple4.HasValue)
												{
													List<CardInstance> list3 = _state.Players[tuple4.Value.Item1].Ranks[tuple4.Value.Item2];
													if (tuple4.Value.Item3 > 0)
													{
														AddTurnBuff(_state, list3[tuple4.Value.Item3 - 1].InstanceId, "ATK", 2);
														AddTurnBuff(_state, list3[tuple4.Value.Item3 - 1].InstanceId, "AC", 2);
													}
													if (tuple4.Value.Item3 + 1 < list3.Count)
													{
														AddTurnBuff(_state, list3[tuple4.Value.Item3 + 1].InstanceId, "ATK", 2);
														AddTurnBuff(_state, list3[tuple4.Value.Item3 + 1].InstanceId, "AC", 2);
													}
												}
											}
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
		if (action is PlayCardAction playCardAction)
		{
		PlayerState playerState6 = _state.Players[playCardAction.PlayerIndex];
		int num6 = playerState6.Hand.FindIndex((CardInstance c) => c.InstanceId == playCardAction.CardInstanceId);
		if (num6 < 0)
		{
			return;
		}
		CardInstance card = playerState6.Hand[num6];
		playerState6.Hand.RemoveAt(num6);
		CardDefinition definition5 = _cardDb.GetDefinition(card.DefinitionId);
		switch (definition5?.Type ?? CardType.Character)
		{
		case CardType.Character:
		case CardType.Token:
		{
			int num13 = playCardAction.TargetRank ?? 1;
			ReadyState readyState = ReadyState.Ready;
			if (definition5 != null && playerState6.WarlordInstanceId.HasValue)
			{
				CardInstance? cardInstance8 = null;
				foreach (List<CardInstance> value7 in playerState6.Ranks.Values)
				{
					foreach (CardInstance item9 in value7)
					{
						if (item9.InstanceId == playerState6.WarlordInstanceId.Value)
						{
							cardInstance8 = item9;
							break;
						}
					}
					if (cardInstance8.HasValue)
					{
						break;
					}
				}
				if (cardInstance8.HasValue)
				{
					CardDefinition definition6 = _cardDb.GetDefinition(cardInstance8.Value.DefinitionId);
					if (definition6 != null)
					{
						string effectiveFaction = definition6.GetEffectiveFaction();
						string effectiveFaction2 = definition5.GetEffectiveFaction();
						if (!effectiveFaction2.Equals("Mercenary", StringComparison.OrdinalIgnoreCase) && !definition5.Traits.Contains("Mercenary", StringComparer.OrdinalIgnoreCase) && !effectiveFaction2.Equals(effectiveFaction, StringComparison.OrdinalIgnoreCase))
						{
							readyState = ReadyState.Stunned;
						}
					}
				}
			}
			CardInstance cardInstance9 = card with
			{
				Zone = CardZone.Board,
				ReadyState = readyState,
				Rank = num13
			};
			if (!playerState6.Ranks.TryGetValue(num13, out List<CardInstance> value4))
			{
				value4 = new List<CardInstance>();
				playerState6.Ranks[num13] = value4;
			}
			value4.Add(cardInstance9);
			TriggerCharacterEnterReacts(cardInstance9, num13, playCardAction.PlayerIndex);
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
				(int, int, int) itemPassiveStats = GetItemPassiveStats(definition5);
				foreach (PlayerState player3 in _state.Players)
				{
					foreach (List<CardInstance> value8 in player3.Ranks.Values)
					{
						for (int num14 = 0; num14 < value8.Count; num14++)
						{
							if (value8[num14].InstanceId == targetCharId.Value)
							{
								CardInstance cardInstance10 = value8[num14];
								List<int> attachedItemIds = new List<int>(cardInstance10.AttachedItemIds) { card.InstanceId };
								value8[num14] = cardInstance10 with
								{
									AttachedItemIds = attachedItemIds,
									CurrentArmorClass = cardInstance10.CurrentArmorClass + itemPassiveStats.Item2,
									CurrentHitPoints = cardInstance10.CurrentHitPoints + itemPassiveStats.Item3
								};
								break;
							}
						}
					}
				}
				if (!playerState6.AttachedItems.Any((CardInstance c) => c.InstanceId == attachedCard.InstanceId))
				{
					playerState6.AttachedItems.Add(attachedCard);
				}
			}
			else
			{
				CardInstance discardedItem = card with
				{
					Zone = CardZone.Discard
				};
				if (!playerState6.DiscardPile.Any((CardInstance c) => c.InstanceId == discardedItem.InstanceId))
				{
					playerState6.DiscardPile.Add(discardedItem);
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
			playerState6.DiscardPile.Add(item3);
			int num7 = playCardAction.PerformerInstanceId ?? FindDefaultPerformer(playerState6, definition5);
			if (!(definition5 != null) || definition5.Abilities.Count <= 0)
			{
				break;
			}
			int num8 = playCardAction.AbilityIndex ?? playCardAction.ChoiceIndex ?? ((_state.Priority.IsReactWindowOpen && definition5.Abilities.Any((AbilityDefinition a) => a.IsReact)) ? definition5.Abilities.Select((AbilityDefinition a, int idx) => (a: a, idx: idx)).FirstOrDefault(((AbilityDefinition a, int idx) x) => x.a.IsReact).idx : 0);
			if (num8 < 0 || num8 >= definition5.Abilities.Count)
			{
				num8 = 0;
			}
			AbilityDefinition abilityDefinition2 = definition5.Abilities[num8];
			if (abilityDefinition2.RequiresSpend || Regex.IsMatch(definition5.RulesText, "\\bSpend\\s+(?:Limited\\s+)?(?:Order|React):", RegexOptions.IgnoreCase))
			{
				CardLocator.FindAndMutate(_state, num7, delegate(ref CardInstance c)
				{
					c.ReadyState = ReadyState.Spent;
				});
			}
			int[] targetInstanceIds = ((!playCardAction.TargetCharacterId.HasValue) ? Array.Empty<int>() : new int[1] { playCardAction.TargetCharacterId.Value });
			CardInstance? cardInstance7 = CardLocator.FindCard(_state, num7);
			int value3 = 0;
			if (cardInstance7.HasValue)
			{
				value3 = GetEffectiveLevel(_state, cardInstance7.Value, _cardDb);
			}
			if (definition5 != null)
			{
				if (definition5.Name.Equals("Assassin's Strike", StringComparison.OrdinalIgnoreCase))
				{
					AddTurnBuff(_state, num7, "ADVANTAGE", 1);
					AddTurnBuff(_state, num7, "WOUND_BONUS", 1);
				}
				else if (definition5.Name.Equals("Cunning Shot", StringComparison.OrdinalIgnoreCase))
				{
					AddTurnBuff(_state, num7, "CUNNING_SHOT_PULL", 1);
				}
				else if (definition5.Name.Equals("Dupe", StringComparison.OrdinalIgnoreCase))
				{

					(int, int, int)? tuple5 = FindCharacterPosition(_state, num7);
					if (tuple5.HasValue)
					{
						List<CardInstance> list4 = _state.Players[tuple5.Value.Item1].Ranks[tuple5.Value.Item2];
						if (list4.Count > 1)
						{
							int oldIdx = tuple5.Value.Item3;
							int newIdx = (oldIdx + 1) % list4.Count;
							CardInstance item4 = list4[oldIdx];
							list4.RemoveAt(oldIdx);
							list4.Insert(newIdx, item4);
							AddTurnBuff(_state, num7, "DUPE_ACTIVE", 1);
						}
					}
				}
				else if (definition5.Name.Equals("Gift From Above", StringComparison.OrdinalIgnoreCase))
				{
					int instanceId = playCardAction.TargetCharacterId ?? num7;
					AddTurnBuff(_state, instanceId, "ATK", value3);
					AddTurnBuff(_state, instanceId, "AC", value3);
				}
				else if (definition5.Name.Equals("Quick Strength", StringComparison.OrdinalIgnoreCase))
				{
					int instanceId2 = playCardAction.TargetCharacterId ?? num7;
					AddTurnBuff(_state, instanceId2, "ATK", value3);
				}
				else if (definition5.Name.Equals("Quick Shielding", StringComparison.OrdinalIgnoreCase))
				{
					int instanceId3 = playCardAction.TargetCharacterId ?? num7;
					AddTurnBuff(_state, instanceId3, "AC", value3);
				}
				else if (definition5.Name.Equals("Even the Odds", StringComparison.OrdinalIgnoreCase))
				{
					int num9 = 0;
					(int, int, int)? tuple6 = FindCharacterPosition(_state, num7);
					if (tuple6.HasValue)
					{
						int item5 = tuple6.Value.Item2;
						for (int num10 = 0; num10 < _state.Players.Count; num10++)
						{
							if (num10 == playCardAction.PlayerIndex)
							{
								continue;
							}
							foreach (var (num12, list6) in _state.Players[num10].Ranks)
							{
								if (BoardGeometry.GetCrossBoardDistance(item5, num12) <= 1)
								{
									num9 += list6.Count;
								}
							}
						}
					}
					AddTurnBuff(_state, num7, "ATK", num9);
					AddTurnBuff(_state, num7, "AC", num9);
					if (num9 >= 5)
					{
						AddTurnBuff(_state, num7, "WARD", 1);
					}
				}
				else if (definition5.Name.Equals("Steady Grip", StringComparison.OrdinalIgnoreCase))
				{
					AddTurnBuff(_state, num7, "TRUE_STRIKE", 1);
				}
				else if (definition5.Name.Equals("Spiritual Weapon", StringComparison.OrdinalIgnoreCase))
				{
					AddTurnBuff(_state, num7, "REACH", 1);
				}
				else if (definition5.Name.Equals("Leveled!", StringComparison.OrdinalIgnoreCase))
				{
					playerState6.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == card.InstanceId);
					playerState6.BanishPile.Add(card with
					{
						Zone = CardZone.Banished
					});
				}
			}
			_resolver.Push(new AbilityActivatedEvent
			{
				SourceInstanceId = num7,
				AbilityIndex = num8,
				TargetInstanceIds = targetInstanceIds,
				Ability = abilityDefinition2,
				OwnerPlayerIndex = playCardAction.PlayerIndex
			});
			break;
		}
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
		for (; _resolver.PendingCount > 0; _resolver.Resolve(_state))
		{
			GameEvent gameEvent = _resolver.Peek();
			if (gameEvent is CharacterDefeatedEvent cde && cde.BanishInsteadOfDiscard)
			{
				continue;
			}
			if ((!EventResolver.RequiresReactWindow(gameEvent) && !(gameEvent is CharacterDefeatedEvent)) || (gameEvent is AbilityActivatedEvent abilityActivatedEvent && abilityActivatedEvent.Ability.SplitSecond))
			{
				continue;
			}
			bool num;
			if (!(gameEvent is CharacterDefeatedEvent))
			{
				if (!_state.AutoPassWhenNoReactsAvailable)
				{
					goto IL_0078;
				}
				num = HasAnyLegalReact(_state);
			}
			else
			{
				num = HasAnyLegalReact(_state);
			}
			if (!num)
			{
				continue;
			}
			goto IL_0078;
			IL_0078:
			_state.Priority.IsReactWindowOpen = true;
			_state.Priority.ConsecutivePasses = 0;
			_state.Priority.HasPassed = false;
			if (_state.Players.Count > 0)
			{
				int num2 = actingPlayerIndex ?? _state.ActivePlayerIndex;
				int initPriority = _state.InitiativeWinnerIndex ?? ((num2 + 1) % _state.Players.Count);
				_state.Priority.ActivePlayerIndex = initPriority % _state.Players.Count;
			}
			SyncPendingEvents();
			return;
		}
		CloseReactWindow();
		SyncPendingEvents();
	}

	private void ResolveReactWindowStack()
	{
		while (_resolver.PendingCount > 0)
		{
			_resolver.Resolve(_state);
		}
		CloseReactWindow();
		SyncPendingEvents();
		if (_state.CurrentPhase == TurnPhase.Order)
		{
			_state.TurnState.RecordAction();
		}
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
		foreach (List<CardInstance> value2 in player.Ranks.Values)
		{
			if (value2.Count > 0)
			{
				return value2[0].InstanceId;
			}
		}
		return 0;
	}

}
