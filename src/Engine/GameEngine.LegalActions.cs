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
	public IReadOnlyList<IPlayerAction> GetLegalActions(int playerId)
	{
		return GetLegalActions(_state, playerId);
	}

	public IReadOnlyList<IPlayerAction> GetLegalActions(GameState state, int playerId)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (state.IsGameOver || playerId < 0 || playerId >= state.Players.Count)
		{
			return Array.Empty<IPlayerAction>();
		}
		List<IPlayerAction> list = new List<IPlayerAction>();
		PlayerState playerState = state.Players[playerId];
		if (!state.Priority.IsReactWindowOpen && state.PendingPrompt != null)
		{
			if (state.PendingPrompt.Attacker.OwnerPlayerIndex != playerId)
			{
				return Array.Empty<IPlayerAction>();
			}
			foreach (CardInstance legalTarget in state.PendingPrompt.LegalTargets)
			{
				list.Add(new SelectStrikeTargetAction
				{
					PlayerIndex = playerId,
					TargetInstanceId = legalTarget.InstanceId
				});
			}
			list.Add(new SelectStrikeTargetAction
			{
				PlayerIndex = playerId,
				TargetInstanceId = null
			});
			return list;
		}
		if (state.Priority.IsReactWindowOpen)
		{
			if (state.Priority.ActivePlayerIndex != playerId)
			{
				return Array.Empty<IPlayerAction>();
			}
			foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
			{
				foreach (CardInstance item in rankCards)
				{
					if (item.ReadyState == ReadyState.Stunned)
					{
						continue;
					}
					CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
					if ((object)definition != null)
					{
						for (int i = 0; i < definition.Abilities.Count; i++)
						{
							AbilityDefinition abilityDefinition = definition.Abilities[i];
							if (!abilityDefinition.IsReact || (abilityDefinition.RequiresSpend && item.ReadyState != ReadyState.Ready) || (abilityDefinition.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{item.InstanceId}_{i}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{item.InstanceId}_Limited") || item.AbilitiesUsedThisTurn.Contains(i))) || IsAbilityAlreadyPendingOnStack(state, item.InstanceId, i))
							{
								continue;
							}
							foreach (int[] item2 in EnumerateAbilityTargets(state, playerId, item.InstanceId, abilityDefinition))
							{
								UseAbilityAction useAbilityAction = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = item.InstanceId,
									AbilityIndex = i,
									ChoiceIndex = i,
									TargetInstanceIds = item2
								};
								if (CanExecute(useAbilityAction, state).IsSuccess)
								{
									list.Add(useAbilityAction);
								}
							}
						}
					}
					foreach (int attachedItemId in item.AttachedItemIds)
					{
						CardInstance? cardInstance = FindBoardCard(state, attachedItemId) ?? CardLocator.FindCard(state, attachedItemId);
						if (!cardInstance.HasValue || cardInstance.Value.ReadyState == ReadyState.Stunned)
						{
							continue;
						}
						CardDefinition definition2 = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
						if ((object)definition2 == null)
						{
							continue;
						}
						for (int j = 0; j < definition2.Abilities.Count; j++)
						{
							AbilityDefinition abilityDefinition2 = definition2.Abilities[j];
							if (!abilityDefinition2.IsReact || (abilityDefinition2.RequiresSpend && cardInstance.Value.ReadyState != ReadyState.Ready) || (abilityDefinition2.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{cardInstance.Value.InstanceId}_{j}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{cardInstance.Value.InstanceId}_Limited") || cardInstance.Value.AbilitiesUsedThisTurn.Contains(j))) || IsAbilityAlreadyPendingOnStack(state, cardInstance.Value.InstanceId, j))
							{
								continue;
							}
							foreach (int[] item3 in EnumerateAbilityTargets(state, playerId, item.InstanceId, abilityDefinition2))
							{
								UseAbilityAction useAbilityAction2 = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = cardInstance.Value.InstanceId,
									AbilityIndex = j,
									ChoiceIndex = j,
									TargetInstanceIds = item3
								};
								if (CanExecute(useAbilityAction2, state).IsSuccess)
								{
									list.Add(useAbilityAction2);
								}
							}
						}
					}
				}
			}
			foreach (CardInstance item4 in playerState.Hand)
			{
				CardDefinition definition3 = _cardDb.GetDefinition(item4.DefinitionId);
				if ((object)definition3 == null || definition3.Type != CardType.Action)
				{
					continue;
				}
				for (int k = 0; k < definition3.Abilities.Count; k++)
				{
					AbilityDefinition reactAbility = definition3.Abilities[k];
					if (!reactAbility.IsReact)
					{
						continue;
					}
					List<CardInstance> performers = new List<CardInstance>();
					foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
					{
						foreach (CardInstance candidate in rankCards)
						{
							CardDefinition def = _cardDb.GetDefinition(candidate.DefinitionId);
							if (def != null && PlayCardAction.ValidatePerformerRequirements(definition3, def, candidate).IsSuccess && (reactAbility.RequiredPerformerClass == null || (def.Class.HasValue && def.Class.Value.ToString().Contains(reactAbility.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)) || def.Traits.Any((string t) => t.Contains(reactAbility.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase))))
							{
								performers.Add(candidate);
							}
						}
					}
					foreach (CardInstance performer in performers)
					{
						foreach (int? targetId in EnumerateActionCardTargets(state, playerId, reactAbility, performer))
						{
							PlayCardAction playCardAction = new PlayCardAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item4.InstanceId,
								PerformerInstanceId = performer.InstanceId,
								TargetCharacterId = targetId,
								AbilityIndex = k,
								ChoiceIndex = k
							};
							if (CanExecute(playCardAction, state).IsSuccess)
							{
								list.Add(playCardAction);
							}
						}
					}
				}
			}
			list.Add(new PassAction
			{
				PlayerIndex = playerId
			});
			return list;
		}
		if (state.ActivePlayerIndex != playerId)
		{
			return Array.Empty<IPlayerAction>();
		}
		if (state.CurrentPhase == TurnPhase.Ready)
		{
			foreach (CardInstance handCard in playerState.Hand)
			{
				list.Add(new DiscardCardAction
				{
					PlayerIndex = playerId,
					CardInstanceId = handCard.InstanceId
				});
			}
			list.Add(new PassAction
			{
				PlayerIndex = playerId
			});
			return list;
		}
		if (state.CurrentPhase == TurnPhase.Main)
		{
			List<int> validDeployRanks = GetValidDeployRanks(playerState);
			List<int> friendlyBoardCharacterIds = GetFriendlyBoardCharacterIds(playerState);
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (CardInstance item8 in playerState.Hand)
			{
				CardDefinition definition5 = _cardDb.GetDefinition(item8.DefinitionId);
				if ((object)definition5 == null)
				{
					continue;
				}
				switch (definition5.Type)
				{
				case CardType.Character:
				case CardType.Token:
				{
					if (!hashSet.Add(definition5.DefinitionId))
					{
						break;
					}
					int deployRank = ((definition5.Level <= 0) ? 1 : definition5.Level);
					if (validDeployRanks.Contains(deployRank))
					{
						PlayCardAction playCardAction5 = new PlayCardAction
						{
							PlayerIndex = playerId,
							CardInstanceId = item8.InstanceId,
							TargetRank = deployRank
						};
						if (CanExecute(playCardAction5, state).IsSuccess)
						{
							list.Add(playCardAction5);
						}
					}
					break;
				}
				case CardType.Item:
					foreach (int item9 in friendlyBoardCharacterIds)
					{
						PlayCardAction playCardAction4 = new PlayCardAction
						{
							PlayerIndex = playerId,
							CardInstanceId = item8.InstanceId,
							TargetCharacterId = item9
						};
						if (CanExecute(playCardAction4, state).IsSuccess)
						{
							list.Add(playCardAction4);
						}
					}
					break;
				case CardType.Action:
				{
					if (definition5.Abilities.Count != 0 && !definition5.Abilities.Any((AbilityDefinition a) => a.IsActive))
					{
						break;
					}
					List<CardInstance> actionPerformers = new List<CardInstance>();
					foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
					{
						foreach (CardInstance candidate in rankCards)
						{
							CardDefinition candidateDef = _cardDb.GetDefinition(candidate.DefinitionId);
							if (candidateDef != null && PlayCardAction.ValidatePerformerRequirements(definition5, candidateDef, candidate).IsSuccess)
							{
								actionPerformers.Add(candidate);
							}
						}
					}
					if (definition5.Abilities.Count == 0)
					{
						foreach (CardInstance performer in actionPerformers)
						{
							PlayCardAction playCardAction2 = new PlayCardAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item8.InstanceId,
								PerformerInstanceId = performer.InstanceId,
								TargetCharacterId = null
							};
							if (CanExecute(playCardAction2, state).IsSuccess)
							{
								list.Add(playCardAction2);
							}
						}
						break;
					}
					for (int abilityIdx = 0; abilityIdx < definition5.Abilities.Count; abilityIdx++)
					{
						AbilityDefinition ability = definition5.Abilities[abilityIdx];
						if (!ability.IsActive)
						{
							continue;
						}
						foreach (CardInstance performer in actionPerformers)
						{
							CardDefinition performerDef = _cardDb.GetDefinition(performer.DefinitionId);
							if (ability.RequiredPerformerClass != null && performerDef != null && (!performerDef.Class.HasValue || !performerDef.Class.Value.ToString().Contains(ability.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)) && !performerDef.Traits.Any((string t) => t.Contains(ability.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)))
							{
								continue;
							}
							foreach (int? targetId in EnumerateActionCardTargets(state, playerId, ability, performer))
							{
								PlayCardAction playCardAction3 = new PlayCardAction
								{
									PlayerIndex = playerId,
									CardInstanceId = item8.InstanceId,
									PerformerInstanceId = performer.InstanceId,
									TargetCharacterId = targetId,
									AbilityIndex = abilityIdx,
									ChoiceIndex = abilityIdx
								};
								if (CanExecute(playCardAction3, state).IsSuccess)
								{
									list.Add(playCardAction3);
								}
							}
						}
					}
					break;
				}
				}
			}
		}
		TurnPhase currentPhase = state.CurrentPhase;
		if (currentPhase == TurnPhase.Order || currentPhase == TurnPhase.Combat)
		{
			foreach (var (attackerRank, rankAttackers) in playerState.Ranks)
			{
				foreach (CardInstance attacker in rankAttackers)
				{
					if (attacker.ReadyState != ReadyState.Ready)
					{
						continue;
					}
					CardDefinition? attackerDef = _cardDb.GetDefinition(attacker.DefinitionId);
					bool isRanged = (object)attackerDef != null && attackerDef.Attack?.Strikes.Any((StrikeDescriptor s) => s.IsRanged) == true;
					int maxMeleeReach = (CharacterHasReach(attacker, state, _cardDb) ? 3 : 2);
					foreach (PlayerState defendingPlayer in state.Players)
					{
						if (defendingPlayer.PlayerIndex == playerId)
						{
							continue;
						}
						foreach (var (defenderRank, rankDefenders) in defendingPlayer.Ranks)
						{
							int crossDist = BoardGeometry.GetCrossBoardDistance(attackerRank, defenderRank);
							if (isRanged)
							{
								if (!BoardGeometry.IsRangedValid(attackerRank, defenderRank, 2))
								{
									continue;
								}
							}
							else if (crossDist > maxMeleeReach - 1)
							{
								continue;
							}
							foreach (CardInstance target in rankDefenders)
							{
								AttackAction attackAction = new AttackAction
								{
									PlayerIndex = playerId,
									AttackerInstanceId = attacker.InstanceId,
									TargetInstanceId = target.InstanceId
								};
								if (CanExecute(attackAction, state).IsSuccess)
								{
									list.Add(attackAction);
								}
							}
						}
					}
				}
			}
			foreach (var (currentRank, rankCharacters) in playerState.Ranks)
			{
				foreach (CardInstance item16 in rankCharacters)
				{
					if (item16.ReadyState != ReadyState.Ready)
					{
						continue;
					}
					if (rankCharacters.Count > 1)
					{
						int currentSlot = rankCharacters.FindIndex((CardInstance c) => c.InstanceId == item16.InstanceId);
						for (int slot = 0; slot < rankCharacters.Count; slot++)
						{
							if (slot != currentSlot)
							{
								MarchAction marchWithin = new MarchAction
								{
									PlayerIndex = playerId,
									CardInstanceId = item16.InstanceId,
									SourceRank = currentRank,
									DestinationRank = currentRank,
									TargetSlotIndex = slot
								};
								if (CanExecute(marchWithin, state).IsSuccess)
								{
									list.Add(marchWithin);
								}
							}
						}
					}
					if (currentRank > 1)
					{
						int destRank = currentRank - 1;
						int destCount = playerState.Ranks.TryGetValue(destRank, out var destCards) ? destCards.Count : 0;
						if (destCount == 0)
						{
							JourneyAction journeyAction = new JourneyAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item16.InstanceId,
								SourceRank = currentRank,
								DestinationRank = destRank,
								TargetSlotIndex = 0
							};
							if (CanExecute(journeyAction, state).IsSuccess)
							{
								list.Add(journeyAction);
							}
						}
						else
						{
							for (int slot = 0; slot <= destCount; slot++)
							{
								JourneyAction journeyAction = new JourneyAction
								{
									PlayerIndex = playerId,
									CardInstanceId = item16.InstanceId,
									SourceRank = currentRank,
									DestinationRank = destRank,
									TargetSlotIndex = slot
								};
								if (CanExecute(journeyAction, state).IsSuccess)
								{
									list.Add(journeyAction);
								}
							}
						}
					}
					int fRank = currentRank + 1;
					int fCount = playerState.Ranks.TryGetValue(fRank, out var fCards) ? fCards.Count : 0;
					if (fCount == 0)
					{
						JourneyAction journeyAction2 = new JourneyAction
						{
							PlayerIndex = playerId,
							CardInstanceId = item16.InstanceId,
							SourceRank = currentRank,
							DestinationRank = fRank,
							TargetSlotIndex = 0
						};
						if (CanExecute(journeyAction2, state).IsSuccess)
						{
							list.Add(journeyAction2);
						}
					}
					else
					{
						for (int slot = 0; slot <= fCount; slot++)
						{
							JourneyAction journeyAction2 = new JourneyAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item16.InstanceId,
								SourceRank = currentRank,
								DestinationRank = fRank,
								TargetSlotIndex = slot
							};
							if (CanExecute(journeyAction2, state).IsSuccess)
							{
								list.Add(journeyAction2);
							}
						}
					}
				}
			}
			foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
			{
				foreach (CardInstance character in rankCards)
				{
					if (character.ReadyState == ReadyState.Stunned)
					{
						continue;
					}
					CardDefinition charDef = _cardDb.GetDefinition(character.DefinitionId);
					if ((object)charDef != null)
					{
						for (int abilityIdx = 0; abilityIdx < charDef.Abilities.Count; abilityIdx++)
						{
							AbilityDefinition abilityDef = charDef.Abilities[abilityIdx];
							if (!abilityDef.IsActive || (abilityDef.RequiresSpend && character.ReadyState != ReadyState.Ready) || (abilityDef.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{character.InstanceId}_{abilityIdx}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{character.InstanceId}_Limited") || character.AbilitiesUsedThisTurn.Contains(abilityIdx))))
							{
								continue;
							}
							foreach (int[] targets in EnumerateAbilityTargets(state, playerId, character.InstanceId, abilityDef))
							{
								UseAbilityAction useAbilityAction3 = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = character.InstanceId,
									AbilityIndex = abilityIdx,
									ChoiceIndex = abilityIdx,
									TargetInstanceIds = targets
								};
								if (CanExecute(useAbilityAction3, state).IsSuccess)
								{
									list.Add(useAbilityAction3);
								}
							}
						}
					}
					foreach (int attachedItemId2 in character.AttachedItemIds)
					{
						CardInstance? attachedItemCard = FindBoardCard(state, attachedItemId2) ?? CardLocator.FindCard(state, attachedItemId2);
						if (!attachedItemCard.HasValue || attachedItemCard.Value.ReadyState == ReadyState.Stunned)
						{
							continue;
						}
						CardDefinition attachedItemDef = _cardDb.GetDefinition(attachedItemCard.Value.DefinitionId);
						if ((object)attachedItemDef == null)
						{
							continue;
						}
						for (int itemAbilityIdx = 0; itemAbilityIdx < attachedItemDef.Abilities.Count; itemAbilityIdx++)
						{
							AbilityDefinition itemAbilityDef = attachedItemDef.Abilities[itemAbilityIdx];
							if (!itemAbilityDef.IsActive || (itemAbilityDef.RequiresSpend && attachedItemCard.Value.ReadyState != ReadyState.Ready) || (itemAbilityDef.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{attachedItemCard.Value.InstanceId}_{itemAbilityIdx}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{attachedItemCard.Value.InstanceId}_Limited") || attachedItemCard.Value.AbilitiesUsedThisTurn.Contains(itemAbilityIdx))))
							{
								continue;
							}
							foreach (int[] itemTargets in EnumerateAbilityTargets(state, playerId, character.InstanceId, itemAbilityDef))
							{
								UseAbilityAction useAbilityAction4 = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = attachedItemCard.Value.InstanceId,
									AbilityIndex = itemAbilityIdx,
									ChoiceIndex = itemAbilityIdx,
									TargetInstanceIds = itemTargets
								};
								if (CanExecute(useAbilityAction4, state).IsSuccess)
								{
									list.Add(useAbilityAction4);
								}
							}
						}
					}
				}
			}
		}
		PassAction passAction = new PassAction
		{
			PlayerIndex = playerId
		};
		if (CanExecute(passAction, state).IsSuccess)
		{
			list.Add(passAction);
		}
		return list;
	}

	private static List<int> GetValidDeployRanks(PlayerState player)
	{
		List<int> validRanks = new List<int> { 1 };
		int maxRank = player.Ranks.Keys.DefaultIfEmpty(0).Max();
		for (int r = 2; r <= maxRank + 1; r++)
		{
			if (player.Ranks.TryGetValue(r - 1, out List<CardInstance> prevRankCards) && prevRankCards.Count > 0 && (player.Ranks.TryGetValue(r, out List<CardInstance> currRankCards) ? currRankCards.Count : 0) + 1 <= prevRankCards.Count)
			{
				validRanks.Add(r);
			}
		}
		return validRanks;
	}

	private IEnumerable<int[]> EnumerateAbilityTargets(GameState state, int playerId, int sourceInstanceId, AbilityDefinition ability)
	{
		int performerId = ResolvePerformerCharacterId(state, sourceInstanceId, _cardDb);
		CardInstance? cardInstance = FindBoardCard(state, performerId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int sourceRank = cardInstance.Value.Rank ?? 1;
		if (ability.IsReact)
		{
			GameEvent topPendingEvent = GetTopPendingEvent(state);
			if (topPendingEvent is MarchEvent me && (me.CardInstanceId == sourceInstanceId || me.CardInstanceId == performerId))
			{
				sourceRank = me.DestinationRank;
			}
			if (topPendingEvent != null)
			{
				CardInstance? sourceCard = CardLocator.FindCard(state, sourceInstanceId) ?? cardInstance;
				if (!IsReactTimingAndConditionMet(ability, sourceCard.Value, state, null, out string _))
				{
					yield break;
				}
				if (topPendingEvent is MarchEvent marchEvent && ability.Timing == ReactTiming.AfterMove)
				{
					if (ability.TargetScope == TargetScope.Opposing)
					{
						if (marchEvent.PlayerIndex == playerId)
						{
							yield break;
						}
						if (ability.Effects.OfType<JourneyEffect>().Any())
						{
							yield return Array.Empty<int>();
							yield break;
						}
						if (ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect)))
						{
							yield return new int[1] { marchEvent.CardInstanceId };
							yield break;
						}
						yield return new int[1] { marchEvent.CardInstanceId };
						yield break;
					}
					else if (ability.TargetScope == TargetScope.Self)
					{
						if (marchEvent.CardInstanceId != performerId)
						{
							yield break;
						}
						if (!ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect)))
						{
							yield return new int[1] { marchEvent.CardInstanceId };
							yield break;
						}
					}
					else if (marchEvent.PlayerIndex != playerId)
					{
						yield break;
					}
					else if (!ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect)))
					{
						yield return new int[1] { marchEvent.CardInstanceId };
						yield break;
					}
				}
				if (topPendingEvent is DealDamageEvent dealDamageEvent && (ability.Timing == ReactTiming.BeforeSuffersWounds || ability.Timing == ReactTiming.AfterSuffersWounds) && !ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect) ? true : false))
				{
					yield return new int[1] { dealDamageEvent.TargetInstanceId };
					yield break;
				}
				if (topPendingEvent is DeclareAttackEvent declareAttackEvent && ability.Timing == ReactTiming.AfterAttack)
				{
					if (ability.TargetScope == TargetScope.WithinRankDistance || ability.TargetScope == TargetScope.Opposing || (ability.Description?.Contains("targeting this character", StringComparison.OrdinalIgnoreCase) ?? false))
					{
						yield return new int[1] { declareAttackEvent.AttackerInstanceId };
						yield break;
					}
				}
			}
			else if (ability.Timing != ReactTiming.None)
			{
				yield break;
			}
		}
		CardInstance? sourceCardInstance = CardLocator.FindCard(state, sourceInstanceId);
		CardDefinition? sourceCardDef = sourceCardInstance.HasValue ? _cardDb.GetDefinition(sourceCardInstance.Value.DefinitionId) : null;
		bool isHodsLegacy = CardScriptRegistry.UsesLowerLevelFactionTargeting(sourceCardDef);
		bool isHuntingStaff = CardScriptRegistry.UsesAdjacentMoveForwardTargeting(sourceCardDef);

		if (isHodsLegacy || (ability.Description?.Contains("Hod's Legacy", StringComparison.OrdinalIgnoreCase) ?? false) || ((ability.Description?.Contains("lower level", StringComparison.OrdinalIgnoreCase) ?? false) && (ability.Description?.Contains("same faction", StringComparison.OrdinalIgnoreCase) ?? false)))
		{
			int bearerLevel = GetEffectiveLevel(state, cardInstance.Value, _cardDb);
			CardDefinition? bearerDef = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
			PlayerState playerState = state.Players[playerId];
			bool readyAbility = ability.Effects.OfType<ChangeReadyStateEffect>().Any((ChangeReadyStateEffect e) => e.TargetState == ReadyState.Ready);
			bool unstunAbility = ability.Effects.OfType<ChangeReadyStateEffect>().Any((ChangeReadyStateEffect e) => e.OnlyIfStunned);
			foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
			{
				foreach (CardInstance ally in rankCards)
				{
					if (ally.InstanceId == performerId)
					{
						continue;
					}
					if (GetEffectiveLevel(state, ally, _cardDb) >= bearerLevel)
					{
						continue;
					}
					CardDefinition? allyDef = _cardDb.GetDefinition(ally.DefinitionId);
					if (!ShareFaction(bearerDef, allyDef))
					{
						continue;
					}
					if (readyAbility && (ally.ReadyState == ReadyState.Stunned || ally.ReadyState == ReadyState.Ready))
					{
						continue;
					}
					if (unstunAbility && ally.ReadyState != ReadyState.Stunned)
					{
						continue;
					}
					yield return new int[1] { ally.InstanceId };
				}
			}
			yield break;
		}

		if (isHuntingStaff || (ability.Description?.Contains("Hunting Staff", StringComparison.OrdinalIgnoreCase) ?? false) || ((ability.Description?.Contains("adjacent character", StringComparison.OrdinalIgnoreCase) ?? false) && (ability.Description?.Contains("moves forward one rank", StringComparison.OrdinalIgnoreCase) ?? false)))
		{
			PlayerState playerState = state.Players[playerId];
			foreach (CardInstance adj in BoardGeometry.GetAdjacentCharacters(playerState, performerId))
			{
				yield return new int[1] { adj.InstanceId };
			}
			yield break;
		}

		if (ability.Effects.OfType<TargetHigherLevelDeverenianJourneyEffect>().Any())
		{
			PlayerState playerState = state.Players[playerId];
			int performerLevel = GetEffectiveLevel(state, cardInstance.Value, _cardDb);
			foreach (var (r, rCards) in playerState.Ranks)
			{
				foreach (var ally in rCards)
				{
					if (ally.InstanceId == sourceInstanceId) continue;
					CardDefinition? def = _cardDb.GetDefinition(ally.DefinitionId);
					if (IsDeverenian(def) && GetEffectiveLevel(state, ally, _cardDb) > performerLevel)
					{
						yield return new int[1] { ally.InstanceId };
					}
				}
			}
			yield break;
		}
		if (ability.Effects.OfType<TargetPerformsStrikeEffect>().Any())
		{
			TargetPerformsStrikeEffect eff = ability.Effects.OfType<TargetPerformsStrikeEffect>().First();
			PlayerState playerState = state.Players[playerId];
			foreach (var (pRank, pCards) in playerState.Ranks)
			{
				foreach (var performer in pCards)
				{
					List<int> candidateTargets = new List<int>();
					int oppPlayerIdx = 1 - playerId;
					if (oppPlayerIdx >= 0 && oppPlayerIdx < state.Players.Count)
					{
						foreach (var (oppRank, oppCards) in state.Players[oppPlayerIdx].Ranks)
						{
							int crossDist = BoardGeometry.GetCrossBoardDistance(pRank, oppRank);
							if (eff.IsRanged ? BoardGeometry.IsRangedValid(pRank, oppRank, 2) : crossDist <= 1)
							{
								candidateTargets.AddRange(oppCards.Select(c => c.InstanceId));
							}
						}
					}
					if (candidateTargets.Count == 0)
					{
						foreach (var (fRank, fCards) in playerState.Ranks)
						{
							int fDist = Math.Max(1, Math.Abs(pRank - fRank));
							if (fDist <= 1)
							{
								candidateTargets.AddRange(fCards.Where(c => c.InstanceId != performer.InstanceId).Select(c => c.InstanceId));
							}
						}
					}

					if (candidateTargets.Count > 0)
					{
						foreach (int targetId in candidateTargets)
						{
							yield return new int[2] { performer.InstanceId, targetId };
						}
					}
					else
					{
						yield return new int[1] { performer.InstanceId };
					}
				}
			}
			yield break;
		}
		if (ability.TargetScope == TargetScope.Self && !ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect) ? true : false))
		{
			yield return new int[1] { performerId };
			yield break;
		}
		if (ability.Description?.Contains("wounded Dwarven Forges character", StringComparison.OrdinalIgnoreCase) ?? false)
		{
			PlayerState playerState = state.Players[playerId];
			foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
			{
				foreach (CardInstance item in rankCards)
				{
					CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
					if (IsDwarf(definition) && IsCharacterWounded(item, definition, state, _cardDb))
					{
						yield return new int[1] { item.InstanceId };
					}
				}
			}
			yield break;
		}
		if (ability.Description?.Contains("non-stunned Dwarven Forges character in a front rank", StringComparison.OrdinalIgnoreCase) ?? false)
		{
			if (!state.Players[playerId].Ranks.TryGetValue(1, out List<CardInstance> frontRankCards))
			{
				yield break;
			}
			foreach (CardInstance item2 in frontRankCards)
			{
				if (item2.ReadyState != ReadyState.Stunned && IsDwarf(_cardDb.GetDefinition(item2.DefinitionId)))
				{
					yield return new int[1] { item2.InstanceId };
				}
			}
			yield break;
		}
		if (ability.Description?.Contains("Your two target characters attack", StringComparison.OrdinalIgnoreCase) ?? false)
		{
			List<int> list = (from c in state.Players[playerId].Ranks.Values.SelectMany((List<CardInstance> r) => r)
				where c.ReadyState == ReadyState.Ready
				select c.InstanceId).ToList();
			if (list.Count >= 2)
			{
				yield return new int[2]
				{
					list[0],
					list[1]
				};
			}
			else if (list.Count == 1)
			{
				yield break; // Pack Tactics requires two distinct targets; a single ready character is not a legal selection.
			}
			yield break;
		}
		if (ability.TargetScope == TargetScope.SameRank)
		{
			if (!state.Players[playerId].Ranks.TryGetValue(sourceRank, out List<CardInstance> sameRankCards))
			{
				yield break;
			}
			foreach (CardInstance item3 in sameRankCards)
			{
				yield return new int[1] { item3.InstanceId };
			}
			yield break;
		}
		if (ability.TargetScope == TargetScope.OtherInSameRank)
		{
			if (!state.Players[playerId].Ranks.TryGetValue(sourceRank, out List<CardInstance> sameRankCards))
			{
				yield break;
			}
			foreach (CardInstance item4 in sameRankCards)
			{
				if (item4.InstanceId != sourceInstanceId)
				{
					yield return new int[1] { item4.InstanceId };
				}
			}
			yield break;
		}
		if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
		{
			int maxDist = ability.MaxRankDistance.Value;
			foreach (PlayerState player in state.Players)
			{
				foreach (var (targetRank, rankCards) in player.Ranks)
				{
					if (Math.Abs(targetRank - sourceRank) > maxDist)
					{
						continue;
					}
					foreach (CardInstance item5 in rankCards)
					{
						yield return new int[1] { item5.InstanceId };
					}
				}
			}
			yield break;
		}
		bool isDamageOrAttack = ability.Effects.Any((IEffect e) => (e is DealDamageEffect || e is ResolveAttackEffect || e is PerformStrikeEffect || e is FireballEffect || e is ShadowBoltsEffect) ? true : false);
		bool isStatOrHeal = ability.Effects.Any((IEffect e) => e is ModifyStatEffect || e.GetType().Name.Contains("Heal", StringComparison.OrdinalIgnoreCase));
		if (isDamageOrAttack)
		{
			bool yielded = false;
			foreach (PlayerState defendingPlayer in state.Players)
			{
				if (defendingPlayer.PlayerIndex == playerId)
				{
					continue;
				}
				foreach (var (defenderRank, defenderRankCards) in defendingPlayer.Ranks)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(sourceRank, defenderRank);
					PerformStrikeEffect? strikeEff = ability.Effects.OfType<PerformStrikeEffect>().FirstOrDefault();
					if (strikeEff != null)
					{
						if (strikeEff.IsRanged)
						{
							int effectiveMax = strikeEff.HasReach ? Math.Max(strikeEff.MaxRange, 3) : strikeEff.MaxRange;
							if (crossDist > effectiveMax)
							{
								continue;
							}
						}
						else
						{
							int maxMelee = (strikeEff.HasReach || (cardInstance.HasValue && CharacterHasReach(cardInstance.Value, state, _cardDb))) ? 2 : 1;
							if (crossDist > maxMelee)
							{
								continue;
							}
						}
					}
					else if (ability.Effects.Any((IEffect e) => (e is FireballEffect || e is ShadowBoltsEffect) ? true : false))
					{
						if (crossDist > 2)
						{
							continue;
						}
					}
					foreach (CardInstance item6 in defenderRankCards)
					{
						if (strikeEff != null && cardInstance.HasValue && IsTargetProtectedByStealth(state, cardInstance.Value, item6, strikeEff.IsRanged, _cardDb))
						{
							continue;
						}
						yielded = true;
						yield return new int[1] { item6.InstanceId };
					}
				}
			}
			if (!yielded)
			{
				PlayerState playerState = state.Players[playerId];
				foreach (var (r, rCards) in playerState.Ranks)
				{
					int fDist = Math.Max(1, Math.Abs(sourceRank - r));
					PerformStrikeEffect? strikeEff = ability.Effects.OfType<PerformStrikeEffect>().FirstOrDefault();
					int maxMelee = ((object)strikeEff != null && strikeEff.HasReach) ? 2 : 1;
					if (fDist <= maxMelee)
					{
						foreach (var ally in rCards)
						{
							if (ally.InstanceId != performerId)
							{
								yielded = true;
								yield return new int[1] { ally.InstanceId };
							}
						}
					}
				}
				if (!yielded)
				{
					yield return Array.Empty<int>();
				}
			}
		}
		else if (isStatOrHeal)
		{
			PlayerState friendlyPlayer = state.Players[playerId];
			bool yielded = false;
			foreach (List<CardInstance> rankCards in friendlyPlayer.Ranks.Values)
			{
				foreach (CardInstance item7 in rankCards)
				{
					yielded = true;
					yield return new int[1] { item7.InstanceId };
				}
			}
			if (!yielded)
			{
				yield return new int[1] { sourceInstanceId };
			}
		}
		else
		{
			yield return Array.Empty<int>();
		}
	}

	private IEnumerable<int?> EnumerateActionCardTargets(GameState state, int playerId, AbilityDefinition ability, CardInstance? performer = null)
	{
		if (state.Priority.IsReactWindowOpen && ability.IsReact)
		{
			GameEvent topPendingEvent = GetTopPendingEvent(state);
			if (topPendingEvent == null && ability.Timing != ReactTiming.None)
			{
				yield break;
			}
			if (ability.Timing == ReactTiming.BeforeStrikeRoll)
			{
				if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent) || (performer.HasValue && ability.TargetScope == TargetScope.Self && declareAttackEvent.AttackerInstanceId != performer.Value.InstanceId))
				{
					yield break;
				}
				if (performer.HasValue && ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
				{
					CardInstance? cardInstance = FindBoardCard(state, declareAttackEvent.AttackerInstanceId);
					if (!cardInstance.HasValue || Math.Abs((cardInstance.Value.Rank ?? 1) - (performer.Value.Rank ?? 1)) > ability.MaxRankDistance.Value)
					{
						yield break;
					}
				}
				CardInstance? defenderCard = FindBoardCard(state, declareAttackEvent.DefenderInstanceId);
				if ((!ability.Effects.OfType<PerformStrikeEffect>().Any((PerformStrikeEffect s) => s.TargetSpentOrStunnedOnly) && !Regex.IsMatch(ability.Description ?? "", "\\b(?:spent\\s+or\\s+stunned|against\\s+a\\s+spent|target\\s+a\\s+spent)\\b", RegexOptions.IgnoreCase)) || !defenderCard.HasValue || defenderCard.Value.ReadyState != ReadyState.Ready)
				{
					yield return declareAttackEvent.AttackerInstanceId;
				}
				yield break;
			}
			if (ability.Timing == ReactTiming.AfterTargetedWithStrike)
			{
				if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent2) || (performer.HasValue && ability.TargetScope == TargetScope.Self && declareAttackEvent2.DefenderInstanceId != performer.Value.InstanceId))
				{
					yield break;
				}
				if (Regex.IsMatch(ability.Description ?? "", "\\bmelee\\s+strike\\b", RegexOptions.IgnoreCase))
				{
					CardInstance? atkCard = FindBoardCard(state, declareAttackEvent2.AttackerInstanceId);
					CardDefinition? atkDef = atkCard.HasValue ? _cardDb.GetDefinition(atkCard.Value.DefinitionId) : null;
					if (atkDef != null && atkDef.Attack?.Strikes.Count > 0 && atkDef.Attack.Strikes.All((StrikeDescriptor s) => s.IsRanged))
					{
						yield break;
					}
				}
				if (Regex.IsMatch(ability.Description ?? "", "\\bmove\\s+to\\s+a\\s+different\\s+position\\s+in\\s+this\\s+rank\\b", RegexOptions.IgnoreCase))
				{
					if (!performer.HasValue || !performer.Value.Rank.HasValue || !state.Players[performer.Value.OwnerPlayerIndex].Ranks.TryGetValue(performer.Value.Rank.Value, out var rankList) || rankList.Count <= 1)
					{
						yield break;
					}
				}
				if (performer.HasValue && ability.TargetScope == TargetScope.OtherInSameRank)
				{
					CardInstance? sameRankTarget = FindBoardCard(state, declareAttackEvent2.DefenderInstanceId);
					if (!sameRankTarget.HasValue || sameRankTarget.Value.OwnerPlayerIndex != performer.Value.OwnerPlayerIndex || sameRankTarget.Value.Rank != performer.Value.Rank || sameRankTarget.Value.InstanceId == performer.Value.InstanceId)
					{
						yield break;
					}
				}
				if (performer.HasValue && ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
				{
					CardInstance? rankDistTarget = FindBoardCard(state, declareAttackEvent2.DefenderInstanceId);
					if (!rankDistTarget.HasValue || Math.Abs((rankDistTarget.Value.Rank ?? 1) - (performer.Value.Rank ?? 1)) > ability.MaxRankDistance.Value)
					{
						yield break;
					}
				}
				yield return declareAttackEvent2.DefenderInstanceId;
				yield break;
			}
			if (ability.Timing == ReactTiming.AfterAttack)
			{
				if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent3))
				{
					yield break;
				}
				if (performer.HasValue && ability.TargetScope == TargetScope.SameRank)
				{
					CardInstance? sameRankDefender = FindBoardCard(state, declareAttackEvent3.DefenderInstanceId);
					if (!sameRankDefender.HasValue || sameRankDefender.Value.OwnerPlayerIndex != performer.Value.OwnerPlayerIndex || sameRankDefender.Value.Rank != performer.Value.Rank)
					{
						yield break;
					}
				}
				yield return declareAttackEvent3.AttackerInstanceId;
				yield break;
			}
			if (ability.Timing == ReactTiming.BeforeDies)
			{
				if (topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent && characterDefeatedEvent.OwnerPlayerIndex == playerId)
				{
					CardInstance? defeatedCard = FindBoardCard(state, characterDefeatedEvent.DefeatedInstanceId);
					if (defeatedCard.HasValue && (!(ability.Description?.Contains("Dwarven Forges", StringComparison.OrdinalIgnoreCase) ?? false) || (IsDwarf(_cardDb.GetDefinition(defeatedCard.Value.DefinitionId)) && defeatedCard.Value.AttachedItemIds.Count != 0)))
					{
						yield return characterDefeatedEvent.DefeatedInstanceId;
					}
				}
				yield break;
			}
			if (ability.Timing == ReactTiming.Kill)
			{
				if (topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent2 && characterDefeatedEvent2.OwnerPlayerIndex != playerId && characterDefeatedEvent2.KillerInstanceId.HasValue)
				{
					yield return characterDefeatedEvent2.KillerInstanceId.Value;
				}
				yield break;
			}
			if (ability.Timing == ReactTiming.BeforeSuffersWounds || ability.Timing == ReactTiming.AfterSuffersWounds)
			{
				if (topPendingEvent is DealDamageEvent dealDamageEvent)
				{
					yield return dealDamageEvent.TargetInstanceId;
				}
				yield break;
			}
			if (ability.Timing == ReactTiming.AfterMove)
			{
				if (!(topPendingEvent is MarchEvent marchEvent))
				{
					yield break;
				}
				if (!ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect) ? true : false))
				{
					yield return marchEvent.CardInstanceId;
					yield break;
				}
			}
			else if (ability.Timing != ReactTiming.None)
			{
				yield break;
			}
		}
		if (ability.Description?.Contains("Your two target characters attack", StringComparison.OrdinalIgnoreCase) ?? false)
		{
			List<int> list = (from c in state.Players[playerId].Ranks.Values.SelectMany((List<CardInstance> r) => r)
				where c.ReadyState == ReadyState.Ready
				select c.InstanceId).ToList();
			if (list.Count >= 2)
			{
				yield return list[0];
			}
			else if (list.Count == 1)
			{
				yield return list[0];
			}
			yield break;
		}
		if (ability.Description?.Contains("Target opponent chooses", StringComparison.OrdinalIgnoreCase) ?? false)
		{
			foreach (PlayerState player in state.Players)
			{
				if (player.PlayerIndex != playerId && player.Ranks.TryGetValue(1, out List<CardInstance> frontCards) && frontCards.Count > 0)
				{
					yield return frontCards[0].InstanceId;
				}
			}
			yield break;
		}
		if (ability.Effects.OfType<TargetHigherLevelDeverenianJourneyEffect>().Any())
		{
			PlayerState playerState = state.Players[playerId];
			int performerLevel = performer.HasValue ? GetEffectiveLevel(state, performer.Value, _cardDb) : 0;
			foreach (var (r, rCards) in playerState.Ranks)
			{
				foreach (var ally in rCards)
				{
					if (performer.HasValue && ally.InstanceId == performer.Value.InstanceId) continue;
					CardDefinition? def = _cardDb.GetDefinition(ally.DefinitionId);
					if (IsDeverenian(def) && GetEffectiveLevel(state, ally, _cardDb) > performerLevel)
					{
						yield return ally.InstanceId;
					}
				}
			}
			yield break;
		}
		if ((ability.Description?.Contains("within one rank", StringComparison.OrdinalIgnoreCase) ?? false) && ability.Effects.Any((IEffect e) => (e is ChangeReadyStateEffect || e is GrantTraitEffect) ? true : false))
		{
			int pRank = performer?.Rank ?? 1;
			PlayerState playerState = state.Players[playerId];
			foreach (var (rankNum, rankCards) in playerState.Ranks)
			{
				if (Math.Abs(rankNum - pRank) > 1)
				{
					continue;
				}
				foreach (CardInstance item in rankCards)
				{
					yield return item.InstanceId;
				}
			}
			yield break;
		}
		bool isDamageOrAttack = ability.Effects.Any((IEffect e) => (e is DealDamageEffect || e is ResolveAttackEffect || e is PerformStrikeEffect || e is FireballEffect || e is ShadowBoltsEffect) ? true : false);
		if (ability.Effects.OfType<DestroyTargetItemEffect>().Any())
		{
			DestroyTargetItemEffect destroyEff = ability.Effects.OfType<DestroyTargetItemEffect>().First();
			int pLevel = performer.HasValue ? GetEffectiveLevel(state, performer.Value, _cardDb) : 0;
			foreach (PlayerState p in state.Players)
			{
				foreach (List<CardInstance> rankList in p.Ranks.Values)
				{
					foreach (CardInstance holder in rankList)
					{
						if (performer.HasValue && GetBoardRankDistance(performer.Value, holder) > destroyEff.MaxRankDistance)
						{
							continue;
						}
						foreach (int itemId in holder.AttachedItemIds)
						{
							CardInstance? itemCard = CardLocator.FindCard(state, itemId);
							CardDefinition? itemDef = itemCard.HasValue ? _cardDb.GetDefinition(itemCard.Value.DefinitionId) : null;
							if (itemDef != null && itemDef.Level < pLevel + destroyEff.LevelOffset)
							{
								yield return itemId;
							}
						}
					}
				}
			}
			yield break;
		}
		if (ability.Effects.OfType<FortitudeSaveEffect>().Any((FortitudeSaveEffect f) => f.PerEquippedItem))
		{
			foreach (PlayerState p in state.Players)
			{
				foreach (List<CardInstance> rankList in p.Ranks.Values)
				{
					foreach (CardInstance candidate in rankList)
					{
						if ((!performer.HasValue || GetBoardRankDistance(performer.Value, candidate) <= 2) && candidate.AttachedItemIds.Count > 0)
						{
							yield return candidate.InstanceId;
						}
					}
				}
			}
			yield break;
		}
		bool isStatOrHeal = ability.Effects.Any((IEffect e) => ((e is ModifyStatEffect || e is HealEffect) ? true : false) || e.GetType().Name.Contains("Heal", StringComparison.OrdinalIgnoreCase));
		if (isDamageOrAttack)
		{
			PerformStrikeEffect performStrikeEffect = ability.Effects.OfType<PerformStrikeEffect>().FirstOrDefault();
			bool targetSpentOnly = performStrikeEffect?.TargetSpentOnly ?? false;
			bool targetStunnedOnly = performStrikeEffect?.TargetStunnedOnly ?? false;
			bool targetSpentOrStunned = ((object)performStrikeEffect != null && performStrikeEffect.TargetSpentOrStunnedOnly) || Regex.IsMatch(ability.Description ?? "", "\\b(?:spent\\s+or\\s+stunned|against\\s+a\\s+spent|target\\s+a\\s+spent)\\b", RegexOptions.IgnoreCase);
			bool isRanged = (performStrikeEffect != null && performStrikeEffect.IsRanged) || ability.Effects.Any((IEffect e) => (e is FireballEffect || e is ShadowBoltsEffect) ? true : false);
			GameEvent topPendingEvt = GetTopPendingEvent(state);
			int performerRank = (topPendingEvt is MarchEvent me && me.CardInstanceId == performer?.InstanceId) ? me.DestinationRank : (performer?.Rank ?? 1);
			int effectiveMaxRanged = (performStrikeEffect != null && performStrikeEffect.HasReach) ? Math.Max(performStrikeEffect.MaxRange, 3) : (performStrikeEffect?.MaxRange ?? 2);
			int maxRange = isRanged ? effectiveMaxRanged : ((performer.HasValue && CharacterHasReach(performer.Value, state, _cardDb)) ? 2 : 1);
			bool yielded = false;
			foreach (PlayerState defendingPlayer in state.Players)
			{
				if (defendingPlayer.PlayerIndex == playerId)
				{
					continue;
				}
				foreach (var (defenderRank, defenderRankCards) in defendingPlayer.Ranks)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(performerRank, defenderRank);
					if (crossDist > maxRange)
					{
						continue;
					}
					foreach (CardInstance item2 in defenderRankCards)
					{
						if (performer.HasValue && IsTargetProtectedByStealth(state, performer.Value, item2, isRanged, _cardDb))
						{
							continue;
						}
						if ((!targetSpentOnly || item2.ReadyState == ReadyState.Spent) && (!targetStunnedOnly || item2.ReadyState == ReadyState.Stunned) && (!targetSpentOrStunned || item2.ReadyState != ReadyState.Ready))
						{
							yielded = true;
							yield return item2.InstanceId;
						}
					}
				}
			}
			if (!yielded)
			{
				PlayerState playerState = state.Players[playerId];
				foreach (var (r, rCards) in playerState.Ranks)
				{
					int fDist = Math.Max(1, Math.Abs(performerRank - r));
					int maxMelee = ((object)performStrikeEffect != null && performStrikeEffect.HasReach) ? 2 : 1;
					if (fDist <= maxMelee)
					{
						foreach (var ally in rCards)
						{
							if (!performer.HasValue || ally.InstanceId != performer.Value.InstanceId)
							{
								yielded = true;
								yield return ally.InstanceId;
							}
						}
					}
				}
				if (!yielded)
				{
					yield return null;
				}
			}
		}
		else if (isStatOrHeal)
		{
			PlayerState friendlyPlayer = state.Players[playerId];
			bool yielded = false;
			foreach (List<CardInstance> rankCards in friendlyPlayer.Ranks.Values)
			{
				foreach (CardInstance item3 in rankCards)
				{
					yielded = true;
					yield return item3.InstanceId;
				}
			}
			if (!yielded)
			{
				yield return null;
			}
		}
		else
		{
			yield return null;
		}
	}

}
