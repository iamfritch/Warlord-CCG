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
		if (state.Priority.IsReactWindowOpen)
		{
			if (state.Priority.ActivePlayerIndex != playerId)
			{
				return Array.Empty<IPlayerAction>();
			}
			foreach (List<CardInstance> value2 in playerState.Ranks.Values)
			{
				foreach (CardInstance item in value2)
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
					List<CardInstance> list2 = new List<CardInstance>();
					foreach (List<CardInstance> value3 in playerState.Ranks.Values)
					{
						foreach (CardInstance item5 in value3)
						{
							CardDefinition definition4 = _cardDb.GetDefinition(item5.DefinitionId);
							if (definition4 != null && PlayCardAction.ValidatePerformerRequirements(definition3, definition4, item5).IsSuccess && (reactAbility.RequiredPerformerClass == null || (definition4.Class.HasValue && definition4.Class.Value.ToString().Contains(reactAbility.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)) || definition4.Traits.Any((string t) => t.Contains(reactAbility.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase))))
							{
								list2.Add(item5);
							}
						}
					}
					foreach (CardInstance item6 in list2)
					{
						foreach (int? item7 in EnumerateActionCardTargets(state, playerId, reactAbility, item6))
						{
							PlayCardAction playCardAction = new PlayCardAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item4.InstanceId,
								PerformerInstanceId = item6.InstanceId,
								TargetCharacterId = item7,
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
					int num2 = ((definition5.Level <= 0) ? 1 : definition5.Level);
					if (validDeployRanks.Contains(num2))
					{
						PlayCardAction playCardAction5 = new PlayCardAction
						{
							PlayerIndex = playerId,
							CardInstanceId = item8.InstanceId,
							TargetRank = num2
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
					List<CardInstance> list3 = new List<CardInstance>();
					foreach (List<CardInstance> value4 in playerState.Ranks.Values)
					{
						foreach (CardInstance item10 in value4)
						{
							CardDefinition definition6 = _cardDb.GetDefinition(item10.DefinitionId);
							if (definition6 != null && PlayCardAction.ValidatePerformerRequirements(definition5, definition6, item10).IsSuccess)
							{
								list3.Add(item10);
							}
						}
					}
					if (definition5.Abilities.Count == 0)
					{
						foreach (CardInstance item11 in list3)
						{
							PlayCardAction playCardAction2 = new PlayCardAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item8.InstanceId,
								PerformerInstanceId = item11.InstanceId,
								TargetCharacterId = null
							};
							if (CanExecute(playCardAction2, state).IsSuccess)
							{
								list.Add(playCardAction2);
							}
						}
						break;
					}
					for (int num = 0; num < definition5.Abilities.Count; num++)
					{
						AbilityDefinition ability = definition5.Abilities[num];
						if (!ability.IsActive)
						{
							continue;
						}
						foreach (CardInstance item12 in list3)
						{
							CardDefinition definition7 = _cardDb.GetDefinition(item12.DefinitionId);
							if (ability.RequiredPerformerClass != null && definition7 != null && (!definition7.Class.HasValue || !definition7.Class.Value.ToString().Contains(ability.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)) && !definition7.Traits.Any((string t) => t.Contains(ability.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)))
							{
								continue;
							}
							foreach (int? item13 in EnumerateActionCardTargets(state, playerId, ability, item12))
							{
								PlayCardAction playCardAction3 = new PlayCardAction
								{
									PlayerIndex = playerId,
									CardInstanceId = item8.InstanceId,
									PerformerInstanceId = item12.InstanceId,
									TargetCharacterId = item13,
									AbilityIndex = num,
									ChoiceIndex = num
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
			int key;
			List<CardInstance> value;
			foreach (KeyValuePair<int, List<CardInstance>> rank in playerState.Ranks)
			{
				rank.Deconstruct(out key, out value);
				int num3 = key;
				foreach (CardInstance item14 in value)
				{
					if (item14.ReadyState != ReadyState.Ready)
					{
						continue;
					}
					CardDefinition? definition8 = _cardDb.GetDefinition(item14.DefinitionId);
					bool flag = (object)definition8 != null && definition8.Attack?.Strikes.Any((StrikeDescriptor s) => s.IsRanged) == true;
					int num4 = (CharacterHasReach(item14, state, _cardDb) ? 3 : 2);
					foreach (PlayerState player in state.Players)
					{
						if (player.PlayerIndex == playerId)
						{
							continue;
						}
						foreach (KeyValuePair<int, List<CardInstance>> rank2 in player.Ranks)
						{
							rank2.Deconstruct(out key, out value);
							int num5 = key;
							List<CardInstance> list4 = value;
							int crossDist = BoardGeometry.GetCrossBoardDistance(num3, num5);
							if (flag)
							{
								if (!BoardGeometry.IsRangedValid(num3, num5, 2))
								{
									continue;
								}
							}
							else if (crossDist > num4 - 1)
							{
								continue;
							}
							foreach (CardInstance item15 in list4)
							{
								AttackAction attackAction = new AttackAction
								{
									PlayerIndex = playerId,
									AttackerInstanceId = item14.InstanceId,
									TargetInstanceId = item15.InstanceId
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
			foreach (KeyValuePair<int, List<CardInstance>> rank3 in playerState.Ranks)
			{
				rank3.Deconstruct(out key, out value);
				int num6 = key;
				foreach (CardInstance item16 in value)
				{
					if (item16.ReadyState != ReadyState.Ready)
					{
						continue;
					}
					if (value.Count > 1)
					{
						int currentSlot = value.FindIndex((CardInstance c) => c.InstanceId == item16.InstanceId);
						for (int slot = 0; slot < value.Count; slot++)
						{
							if (slot != currentSlot)
							{
								MarchAction marchWithin = new MarchAction
								{
									PlayerIndex = playerId,
									CardInstanceId = item16.InstanceId,
									SourceRank = num6,
									DestinationRank = num6,
									TargetSlotIndex = slot
								};
								if (CanExecute(marchWithin, state).IsSuccess)
								{
									list.Add(marchWithin);
								}
							}
						}
					}
					if (num6 > 1)
					{
						int destRank = num6 - 1;
						int destCount = playerState.Ranks.TryGetValue(destRank, out var destCards) ? destCards.Count : 0;
						if (destCount == 0)
						{
							JourneyAction journeyAction = new JourneyAction
							{
								PlayerIndex = playerId,
								CardInstanceId = item16.InstanceId,
								SourceRank = num6,
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
									SourceRank = num6,
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
					int fRank = num6 + 1;
					int fCount = playerState.Ranks.TryGetValue(fRank, out var fCards) ? fCards.Count : 0;
					if (fCount == 0)
					{
						JourneyAction journeyAction2 = new JourneyAction
						{
							PlayerIndex = playerId,
							CardInstanceId = item16.InstanceId,
							SourceRank = num6,
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
								SourceRank = num6,
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
			foreach (List<CardInstance> value5 in playerState.Ranks.Values)
			{
				foreach (CardInstance item17 in value5)
				{
					if (item17.ReadyState == ReadyState.Stunned)
					{
						continue;
					}
					CardDefinition definition9 = _cardDb.GetDefinition(item17.DefinitionId);
					if ((object)definition9 != null)
					{
						for (int num7 = 0; num7 < definition9.Abilities.Count; num7++)
						{
							AbilityDefinition abilityDefinition3 = definition9.Abilities[num7];
							if (!abilityDefinition3.IsActive || (abilityDefinition3.RequiresSpend && item17.ReadyState != ReadyState.Ready) || (abilityDefinition3.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{item17.InstanceId}_{num7}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{item17.InstanceId}_Limited") || item17.AbilitiesUsedThisTurn.Contains(num7))))
							{
								continue;
							}
							foreach (int[] item18 in EnumerateAbilityTargets(state, playerId, item17.InstanceId, abilityDefinition3))
							{
								UseAbilityAction useAbilityAction3 = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = item17.InstanceId,
									AbilityIndex = num7,
									ChoiceIndex = num7,
									TargetInstanceIds = item18
								};
								if (CanExecute(useAbilityAction3, state).IsSuccess)
								{
									list.Add(useAbilityAction3);
								}
							}
						}
					}
					foreach (int attachedItemId2 in item17.AttachedItemIds)
					{
						CardInstance? cardInstance2 = FindBoardCard(state, attachedItemId2) ?? CardLocator.FindCard(state, attachedItemId2);
						if (!cardInstance2.HasValue || cardInstance2.Value.ReadyState == ReadyState.Stunned)
						{
							continue;
						}
						CardDefinition definition10 = _cardDb.GetDefinition(cardInstance2.Value.DefinitionId);
						if ((object)definition10 == null)
						{
							continue;
						}
						for (int num8 = 0; num8 < definition10.Abilities.Count; num8++)
						{
							AbilityDefinition abilityDefinition4 = definition10.Abilities[num8];
							if (!abilityDefinition4.IsActive || (abilityDefinition4.RequiresSpend && cardInstance2.Value.ReadyState != ReadyState.Ready) || (abilityDefinition4.IsLimited && (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{cardInstance2.Value.InstanceId}_{num8}") || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{cardInstance2.Value.InstanceId}_Limited") || cardInstance2.Value.AbilitiesUsedThisTurn.Contains(num8))))
							{
								continue;
							}
							foreach (int[] item19 in EnumerateAbilityTargets(state, playerId, item17.InstanceId, abilityDefinition4))
							{
								UseAbilityAction useAbilityAction4 = new UseAbilityAction
								{
									PlayerIndex = playerId,
									SourceInstanceId = cardInstance2.Value.InstanceId,
									AbilityIndex = num8,
									ChoiceIndex = num8,
									TargetInstanceIds = item19
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
		List<int> list = new List<int> { 1 };
		int num = player.Ranks.Keys.DefaultIfEmpty(0).Max();
		for (int i = 2; i <= num + 1; i++)
		{
			if (player.Ranks.TryGetValue(i - 1, out List<CardInstance> value) && value.Count > 0 && (player.Ranks.TryGetValue(i, out List<CardInstance> value2) ? value2.Count : 0) + 1 <= value.Count)
			{
				list.Add(i);
			}
		}
		return list;
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
		bool isHodsLegacy = sourceCardDef != null && sourceCardDef.Name.Equals("Hod's Legacy", StringComparison.OrdinalIgnoreCase);
		bool isHuntingStaff = sourceCardDef != null && sourceCardDef.Name.Equals("Hunting Staff", StringComparison.OrdinalIgnoreCase);

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
			foreach (List<CardInstance> value5 in playerState.Ranks.Values)
			{
				foreach (CardInstance item in value5)
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
			if (!state.Players[playerId].Ranks.TryGetValue(1, out List<CardInstance> value))
			{
				yield break;
			}
			foreach (CardInstance item2 in value)
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
			if (!state.Players[playerId].Ranks.TryGetValue(sourceRank, out List<CardInstance> value2))
			{
				yield break;
			}
			foreach (CardInstance item3 in value2)
			{
				yield return new int[1] { item3.InstanceId };
			}
			yield break;
		}
		if (ability.TargetScope == TargetScope.OtherInSameRank)
		{
			if (!state.Players[playerId].Ranks.TryGetValue(sourceRank, out List<CardInstance> value3))
			{
				yield break;
			}
			foreach (CardInstance item4 in value3)
			{
				if (item4.InstanceId != sourceInstanceId)
				{
					yield return new int[1] { item4.InstanceId };
				}
			}
			yield break;
		}
		int key;
		List<CardInstance> value4;
		if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
		{
			int maxDist = ability.MaxRankDistance.Value;
			foreach (PlayerState player in state.Players)
			{
				foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
				{
					rank.Deconstruct(out key, out value4);
					int num = key;
					List<CardInstance> list2 = value4;
					if (Math.Abs(num - sourceRank) > maxDist)
					{
						continue;
					}
					foreach (CardInstance item5 in list2)
					{
						yield return new int[1] { item5.InstanceId };
					}
				}
			}
			yield break;
		}
		bool flag = ability.Effects.Any((IEffect e) => (e is DealDamageEffect || e is ResolveAttackEffect || e is PerformStrikeEffect || e is FireballEffect || e is ShadowBoltsEffect) ? true : false);
		bool flag2 = ability.Effects.Any((IEffect e) => e is ModifyStatEffect || e.GetType().Name.Contains("Heal", StringComparison.OrdinalIgnoreCase));
		if (flag)
		{
			bool yielded = false;
			foreach (PlayerState player2 in state.Players)
			{
				if (player2.PlayerIndex == playerId)
				{
					continue;
				}
				foreach (KeyValuePair<int, List<CardInstance>> rank2 in player2.Ranks)
				{
					rank2.Deconstruct(out key, out value4);
					int num2 = key;
					List<CardInstance> list3 = value4;
					int crossDist = BoardGeometry.GetCrossBoardDistance(sourceRank, num2);
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
					foreach (CardInstance item6 in list3)
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
		else if (flag2)
		{
			PlayerState playerState2 = state.Players[playerId];
			bool yielded = false;
			foreach (List<CardInstance> value6 in playerState2.Ranks.Values)
			{
				foreach (CardInstance item7 in value6)
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
				CardInstance? cardInstance2 = FindBoardCard(state, declareAttackEvent.DefenderInstanceId);
				if ((!ability.Effects.OfType<PerformStrikeEffect>().Any((PerformStrikeEffect s) => s.TargetSpentOrStunnedOnly) && !Regex.IsMatch(ability.Description ?? "", "\\b(?:spent\\s+or\\s+stunned|against\\s+a\\s+spent|target\\s+a\\s+spent)\\b", RegexOptions.IgnoreCase)) || !cardInstance2.HasValue || cardInstance2.Value.ReadyState != ReadyState.Ready)
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
					CardInstance? cardInstance3 = FindBoardCard(state, declareAttackEvent2.DefenderInstanceId);
					if (!cardInstance3.HasValue || cardInstance3.Value.OwnerPlayerIndex != performer.Value.OwnerPlayerIndex || cardInstance3.Value.Rank != performer.Value.Rank || cardInstance3.Value.InstanceId == performer.Value.InstanceId)
					{
						yield break;
					}
				}
				if (performer.HasValue && ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
				{
					CardInstance? cardInstance4 = FindBoardCard(state, declareAttackEvent2.DefenderInstanceId);
					if (!cardInstance4.HasValue || Math.Abs((cardInstance4.Value.Rank ?? 1) - (performer.Value.Rank ?? 1)) > ability.MaxRankDistance.Value)
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
					CardInstance? cardInstance5 = FindBoardCard(state, declareAttackEvent3.DefenderInstanceId);
					if (!cardInstance5.HasValue || cardInstance5.Value.OwnerPlayerIndex != performer.Value.OwnerPlayerIndex || cardInstance5.Value.Rank != performer.Value.Rank)
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
					CardInstance? cardInstance6 = FindBoardCard(state, characterDefeatedEvent.DefeatedInstanceId);
					if (cardInstance6.HasValue && (!(ability.Description?.Contains("Dwarven Forges", StringComparison.OrdinalIgnoreCase) ?? false) || (IsDwarf(_cardDb.GetDefinition(cardInstance6.Value.DefinitionId)) && cardInstance6.Value.AttachedItemIds.Count != 0)))
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
				if (player.PlayerIndex != playerId && player.Ranks.TryGetValue(1, out List<CardInstance> value) && value.Count > 0)
				{
					yield return value[0].InstanceId;
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
		int key;
		List<CardInstance> value2;
		if ((ability.Description?.Contains("within one rank", StringComparison.OrdinalIgnoreCase) ?? false) && ability.Effects.Any((IEffect e) => (e is ChangeReadyStateEffect || e is GrantTraitEffect) ? true : false))
		{
			int pRank = performer?.Rank ?? 1;
			PlayerState playerState = state.Players[playerId];
			foreach (KeyValuePair<int, List<CardInstance>> rank in playerState.Ranks)
			{
				rank.Deconstruct(out key, out value2);
				int num = key;
				List<CardInstance> list2 = value2;
				if (Math.Abs(num - pRank) > 1)
				{
					continue;
				}
				foreach (CardInstance item in list2)
				{
					yield return item.InstanceId;
				}
			}
			yield break;
		}
		bool flag = ability.Effects.Any((IEffect e) => (e is DealDamageEffect || e is ResolveAttackEffect || e is PerformStrikeEffect || e is FireballEffect || e is ShadowBoltsEffect) ? true : false);
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
		bool flag2 = ability.Effects.Any((IEffect e) => ((e is ModifyStatEffect || e is HealEffect) ? true : false) || e.GetType().Name.Contains("Heal", StringComparison.OrdinalIgnoreCase));
		if (flag)
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
			foreach (PlayerState player2 in state.Players)
			{
				if (player2.PlayerIndex == playerId)
				{
					continue;
				}
				foreach (KeyValuePair<int, List<CardInstance>> rank2 in player2.Ranks)
				{
					rank2.Deconstruct(out key, out value2);
					int num2 = key;
					List<CardInstance> list3 = value2;
					int crossDist = BoardGeometry.GetCrossBoardDistance(performerRank, num2);
					if (crossDist > maxRange)
					{
						continue;
					}
					foreach (CardInstance item2 in list3)
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
		else if (flag2)
		{
			PlayerState playerState2 = state.Players[playerId];
			bool yielded = false;
			foreach (List<CardInstance> value3 in playerState2.Ranks.Values)
			{
				foreach (CardInstance item3 in value3)
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
