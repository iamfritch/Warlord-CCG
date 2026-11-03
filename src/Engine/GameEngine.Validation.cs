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
	public ActionResult CanExecute(IPlayerAction action, GameState state)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		ArgumentNullException.ThrowIfNull(state, "state");
		if (state.IsGameOver)
		{
			return ActionResult.Failure("Game is over. No further actions can be executed.");
		}
		switch (action)
		{
			case MarchAction march:
				return ValidateMarch(march, state);
			case PassAction:
				return ValidatePassPriority(action, state);
			case UseAbilityAction useAbility:
				return ValidateUseAbility(useAbility, state);
			case PlayCardAction playCard:
				return ValidatePlayCard(playCard, state);
			case AttackAction attack:
				return ValidateAttackAction(attack, state);
			case DeployStartingArmyAction deployArmy:
				return ValidateDeployStartingArmy(deployArmy, state);
			case DiscardCardAction discardAction:
			{
				PlayerState p = state.Players[discardAction.PlayerIndex];
				if (!p.Hand.Any((CardInstance c) => c.InstanceId == discardAction.CardInstanceId))
				{
					return ActionResult.Failure("Card is not in hand.");
				}
				return ActionResult.Success();
			}
			default:
				return ActionResult.Failure("Unknown action type: " + action.GetType().Name);
		}
	}

	public ActionResult CanExecute(IPlayerAction action)
	{
		return CanExecute(action, _state);
	}

	/// <summary>Rank distance between two board characters (cross-board when controlled by different players).</summary>
	private static int GetBoardRankDistance(CardInstance a, CardInstance b)
	{
		if (!a.Rank.HasValue || !b.Rank.HasValue)
		{
			return 0;
		}
		return a.OwnerPlayerIndex == b.OwnerPlayerIndex
			? Math.Abs(a.Rank.Value - b.Rank.Value)
			: BoardGeometry.GetCrossBoardDistance(a.Rank.Value, b.Rank.Value);
	}

	/// <summary>
	/// Validates range and level limits of an action that destroys a target item (read-only), using the parsed
	/// <see cref="DestroyTargetItemEffect"/> parameters: item holder within MaxRankDistance of the performer and
	/// item level lower than performer level + LevelOffset.
	/// </summary>
	private void ValidateDestroyItemTarget(GameState state, PlayCardAction playCard, CardDefinition actionDef, List<string> errors)
	{
		DestroyTargetItemEffect? effect = actionDef.Abilities.SelectMany((AbilityDefinition a) => a.Effects).OfType<DestroyTargetItemEffect>().FirstOrDefault();
		if (effect == null || !playCard.TargetCharacterId.HasValue || !playCard.PerformerInstanceId.HasValue)
		{
			return;
		}
		CardInstance? performer = FindBoardCard(state, playCard.PerformerInstanceId.Value);
		CardInstance? targetCard = CardLocator.FindCard(state, playCard.TargetCharacterId.Value);
		if (!performer.HasValue || !targetCard.HasValue)
		{
			return;
		}
		CardInstance? holder;
		CardInstance? item;
		if (targetCard.Value.Zone == CardZone.Attached)
		{
			item = targetCard;
			holder = targetCard.Value.AttachedToInstanceId.HasValue ? FindBoardCard(state, targetCard.Value.AttachedToInstanceId.Value) : null;
		}
		else
		{
			holder = targetCard;
			item = targetCard.Value.AttachedItemIds.Count > 0 ? CardLocator.FindCard(state, targetCard.Value.AttachedItemIds[0]) : null;
		}
		if (!holder.HasValue && item.HasValue)
		{
			foreach (PlayerState p in state.Players)
			{
				foreach (List<CardInstance> r in p.Ranks.Values)
				{
					foreach (CardInstance candidate in r)
					{
						if (candidate.AttachedItemIds.Contains(item.Value.InstanceId))
						{
							holder = candidate;
							break;
						}
					}
					if (holder.HasValue) break;
				}
				if (holder.HasValue) break;
			}
		}
		if (holder.HasValue && GetBoardRankDistance(performer.Value, holder.Value) > effect.MaxRankDistance)
		{
			errors.Add($"Target item must be within {effect.MaxRankDistance} ranks of the performer.");
		}
		CardDefinition? itemDef = item.HasValue ? _cardDb.GetDefinition(item.Value.DefinitionId) : null;
		if (itemDef != null && itemDef.Level >= GetEffectiveLevel(state, performer.Value, _cardDb) + effect.LevelOffset)
		{
			errors.Add($"Target item must be lower level than the performer's level + {effect.LevelOffset}.");
		}
	}

	/// <summary>
	/// Validates the complete target set of an ability simultaneously (count, distinctness, existence, control and
	/// single-target filter phrases) so nothing is committed unless every target is legal. Read-only: never mutates state.
	/// </summary>
	private void ValidateMultiTargetSelection(GameState state, UseAbilityAction useAbility, AbilityDefinition ability, List<string> errors)
	{
		int[] targets = useAbility.TargetInstanceIds ?? Array.Empty<int>();
		(int Count, bool MustBeFriendly)? required = RulesParser.ParseRequiredTargets(ability.Description);
		if (required.HasValue)
		{
			if (targets.Length != required.Value.Count)
			{
				errors.Add($"Ability requires exactly {required.Value.Count} targets (got {targets.Length}).");
				return;
			}
			if (targets.Distinct().Count() != targets.Length)
			{
				errors.Add("Each target must be a different character.");
			}
			foreach (int targetId in targets)
			{
				CardInstance? target = FindBoardCard(state, targetId);
				if (!target.HasValue)
				{
					errors.Add($"Target {targetId} not found.");
				}
				else if (required.Value.MustBeFriendly && target.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					errors.Add($"Target {targetId} must be a character you control.");
				}
			}
			return;
		}
	}

	private ActionResult ValidateMarch(MarchAction march, GameState state)
	{
		List<string> list = new List<string>();
		if (state.Priority.IsReactWindowOpen)
		{
			list.Add("Cannot march while a React window is open.");
		}
		if (march.PlayerIndex != state.ActivePlayerIndex)
		{
			list.Add($"It is not Player {march.PlayerIndex}'s turn.");
		}
		if (state.CurrentPhase != TurnPhase.Order && state.CurrentPhase != TurnPhase.Combat)
		{
			list.Add("Marching is only allowed during the Order phase.");
		}
		if (march.PlayerIndex < 0 || march.PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {march.PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		PlayerState playerState = state.Players[march.PlayerIndex];
		if (march.DestinationRank < 1)
		{
			list.Add($"Destination rank {march.DestinationRank} is invalid.");
		}
		if (march.SourceRank == march.DestinationRank)
		{
			if (playerState.Ranks.TryGetValue(march.SourceRank, out List<CardInstance> sameRankCards))
			{
				if (sameRankCards.Count <= 1)
				{
					list.Add("Cannot maneuver within rank when there is only 1 character in the rank.");
				}
				else
				{
					int currentIdx = sameRankCards.FindIndex((CardInstance c) => c.InstanceId == march.CardInstanceId);
					if (!march.TargetSlotIndex.HasValue)
					{
						list.Add("Marching within rank requires a TargetSlotIndex.");
					}
					else if (march.TargetSlotIndex.Value < 0 || march.TargetSlotIndex.Value >= sameRankCards.Count)
					{
						list.Add($"Target slot index {march.TargetSlotIndex.Value} is out of range for rank with {sameRankCards.Count} characters.");
					}
					else if (march.TargetSlotIndex.Value == currentIdx)
					{
						list.Add("Target slot index must change character's position in the rank.");
					}
				}
			}
		}
		else if (march.TargetSlotIndex.HasValue)
		{
			int destCount = playerState.Ranks.TryGetValue(march.DestinationRank, out var destCards) ? destCards.Count : 0;
			if (march.TargetSlotIndex.Value < 0 || march.TargetSlotIndex.Value > destCount)
			{
				list.Add($"Target slot index {march.TargetSlotIndex.Value} is out of range for destination rank with {destCount} characters.");
			}
		}
		if (march.SourceRank != march.DestinationRank && Math.Abs(march.SourceRank - march.DestinationRank) > 1)
		{
			list.Add("Characters can only march one rank at a time.");
		}
		if (!playerState.Ranks.ContainsKey(march.SourceRank))
		{
			list.Add($"No cards at Rank {march.SourceRank}.");
		}
		else
		{
			bool isPresentInRank = false;
			foreach (CardInstance item in playerState.Ranks[march.SourceRank])
			{
				if (item.InstanceId == march.CardInstanceId)
				{
					if (item.ReadyState != ReadyState.Ready)
					{
						list.Add("Cannot march a spent or stunned character.");
					}
					isPresentInRank = true;
					break;
				}
			}
			if (!isPresentInRank)
			{
				list.Add($"Card {march.CardInstanceId} not found at Rank {march.SourceRank}.");
			}
		}
		if (list.Count == 0 && march.SourceRank != march.DestinationRank)
		{
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			foreach (var (key, list3) in playerState.Ranks)
			{
				dictionary[key] = list3.Count;
			}
			dictionary[march.SourceRank] = dictionary.GetValueOrDefault(march.SourceRank) - 1;
			dictionary[march.DestinationRank] = dictionary.GetValueOrDefault(march.DestinationRank) + 1;
			int maxOccupiedRank = (from kvp in dictionary
				where kvp.Value > 0
				select kvp.Key).DefaultIfEmpty(0).Max();
			if (dictionary.GetValueOrDefault(1, 0) == 0 && maxOccupiedRank >= 2)
			{
				list.Add($"Cannot march to Rank {march.DestinationRank}: cannot leave Rank 1 empty when Rank 2 or higher exists.");
			}
			else
			{
				for (int checkRank = 2; checkRank <= maxOccupiedRank; checkRank++)
				{
					int prevRankCount = dictionary.GetValueOrDefault(checkRank - 1, 0);
					int currRankCount = dictionary.GetValueOrDefault(checkRank, 0);
					if (currRankCount > prevRankCount)
					{
						list.Add($"Cannot march to Rank {march.DestinationRank}: formation rule violated (Rank {checkRank} would have {currRankCount} characters, exceeding Rank {checkRank - 1} with {prevRankCount}).");
						break;
					}
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	private static ActionResult ValidatePassPriority(IPlayerAction action, GameState state)
	{
		if (action.PlayerIndex < 0 || action.PlayerIndex >= state.Players.Count)
		{
			return ActionResult.Failure($"Player index {action.PlayerIndex} is out of range.");
		}
		if (state.Priority.IsReactWindowOpen)
		{
			return ActionResult.Success();
		}
		if (action.PlayerIndex != state.ActivePlayerIndex)
		{
			return ActionResult.Failure($"It is not Player {action.PlayerIndex}'s turn.");
		}
		return ActionResult.Success();
	}

	private ActionResult ValidateAttackAction(AttackAction attack, GameState state)
	{
		List<string> list = new List<string>();
		if (state.Priority.IsReactWindowOpen)
		{
			list.Add("Cannot declare an attack while a React window is open.");
		}
		if (attack.PlayerIndex != state.ActivePlayerIndex)
		{
			list.Add($"It is not Player {attack.PlayerIndex}'s turn.");
		}
		if (state.CurrentPhase != TurnPhase.Combat && state.CurrentPhase != TurnPhase.Order)
		{
			list.Add("Attacks are only allowed during the Order phase.");
		}
		if (attack.PlayerIndex < 0 || attack.PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {attack.PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		CardInstance? cardInstance = null;
		int? attackerRank = null;
		foreach (var (rankKey, rankCards) in state.Players[attack.PlayerIndex].Ranks)
		{
			foreach (CardInstance item in rankCards)
			{
				if (item.InstanceId == attack.AttackerInstanceId)
				{
					cardInstance = item;
					attackerRank = rankKey;
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
			list.Add($"Attacker {attack.AttackerInstanceId} not found on the board.");
		}
		else
		{
			if (cardInstance.Value.ReadyState != ReadyState.Ready)
			{
				list.Add("Attacker is spent and cannot attack.");
			}
			if (HasTurnBuff(state, cardInstance.Value.InstanceId, "NO_MELEE_STRIKES") && (_cardDb.GetDefinition(cardInstance.Value.DefinitionId)?.Attack?.Strikes.All((StrikeDescriptor s) => !s.IsRanged) ?? true))
			{
				list.Add("Attacker is unable to perform melee strikes this turn.");
			}
			if (cardInstance.Value.OwnerPlayerIndex != attack.PlayerIndex)
			{
				list.Add("Cannot attack with an opponent's character.");
			}
		}
		CardInstance? defenderCard = null;
		int? defenderRank = null;
		int defenderOwnerIndex = -1;
		foreach (PlayerState player in state.Players)
		{
			foreach (var (rankKey, rankCards) in player.Ranks)
			{
				foreach (CardInstance item2 in rankCards)
				{
					if (item2.InstanceId == attack.TargetInstanceId)
					{
						defenderCard = item2;
						defenderRank = rankKey;
						defenderOwnerIndex = player.PlayerIndex;
						break;
					}
				}
				if (defenderCard.HasValue)
				{
					break;
				}
			}
			if (defenderCard.HasValue)
			{
				break;
			}
		}
		if (!defenderCard.HasValue)
		{
			list.Add($"Target {attack.TargetInstanceId} not found on the board.");
		}
		else if (defenderOwnerIndex == attack.PlayerIndex)
		{
			list.Add("Cannot attack your own character.");
		}
		if (cardInstance.HasValue && defenderCard.HasValue && attackerRank.HasValue && defenderRank.HasValue)
		{
			CardDefinition? definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
			bool isRangedAttack = (object)definition != null && definition.Attack?.Strikes.Any((StrikeDescriptor s) => s.IsRanged) == true;
			if (IsTargetProtectedByStealth(state, cardInstance.Value, defenderCard.Value, isRangedAttack, _cardDb))
			{
				list.Add($"Cannot target '{_cardDb.GetDefinition(defenderCard.Value.DefinitionId)?.Name ?? defenderCard.Value.DefinitionId}' due to Stealth.");
			}
			int rankDistance = BoardGeometry.GetCrossBoardDistance(attackerRank.Value, defenderRank.Value);
			if (isRangedAttack)
			{
				if (!BoardGeometry.IsRangedValid(attackerRank.Value, defenderRank.Value, 2))
				{
					list.Add($"Ranged attacks reach 2 ranks away (distance is {rankDistance}). Attacker is at Rank {attackerRank.Value}, Target is at Rank {defenderRank.Value}.");
				}
			}
			else
			{
				int maxMeleeRankSum = (CharacterHasReach(cardInstance.Value, state, _cardDb) ? 3 : 2);
				if (attackerRank.Value + defenderRank.Value > maxMeleeRankSum)
				{
					list.Add($"Melee attacks require rank sum <= {maxMeleeRankSum}. Attacker is at Rank {attackerRank.Value}, Target is at Rank {defenderRank.Value}.");
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	private ActionResult ValidateDeployStartingArmy(DeployStartingArmyAction deployArmy, GameState state)
	{
		List<string> list = new List<string>();
		if (state.CurrentPhase != TurnPhase.Setup)
		{
			list.Add("Starting army deployment is only allowed during the Setup phase.");
			return ActionResult.Failure(list);
		}
		if (deployArmy.PlayerIndex < 0 || deployArmy.PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {deployArmy.PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		if (deployArmy.Deployments.Count != 6)
		{
			list.Add($"Starting army deployment must contain exactly 6 characters (1 Warlord, 2 Level 2s, 3 Level 1s); found {deployArmy.Deployments.Count}.");
			return ActionResult.Failure(list);
		}
		PlayerState playerState = state.Players[deployArmy.PlayerIndex];
		int warlordCount = 0;
		int level1Count = 0;
		int level2Count = 0;
		CardDefinition cardDefinition = null;
		List<(StartingCharacterDeployment, CardDefinition)> list2 = new List<(StartingCharacterDeployment, CardDefinition)>();
		foreach (StartingCharacterDeployment dep in deployArmy.Deployments)
		{
			CardInstance? cardInstance = playerState.Hand.FirstOrDefault((CardInstance c) => c.InstanceId == dep.CardInstanceId);
			if (!cardInstance.HasValue)
			{
				cardInstance = playerState.Deck.FirstOrDefault((CardInstance c) => c.InstanceId == dep.CardInstanceId);
			}
			if (!cardInstance.HasValue)
			{
				list.Add($"Card instance #{dep.CardInstanceId} not found in player's hand or deck.");
				continue;
			}
			CardDefinition definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
			if (definition == null)
			{
				list.Add("Card definition '" + cardInstance.Value.DefinitionId + "' not found.");
				continue;
			}
			if (definition.Type != CardType.Character)
			{
				list.Add("Card '" + definition.Name + "' is not a Character.");
				continue;
			}
			list2.Add((dep, definition));
			if (definition.IsWarlord)
			{
				warlordCount++;
				cardDefinition = definition;
				if (dep.TargetRank != 3)
				{
					list.Add($"Warlord '{definition.Name}' must be deployed in Rank 3 (found Rank {dep.TargetRank}).");
				}
			}
			else if (definition.Level == 1)
			{
				level1Count++;
				if (dep.TargetRank != 1)
				{
					list.Add($"Level 1 character '{definition.Name}' must be deployed in Rank 1 (found Rank {dep.TargetRank}).");
				}
			}
			else if (definition.Level == 2)
			{
				level2Count++;
				if (dep.TargetRank != 2)
				{
					list.Add($"Level 2 character '{definition.Name}' must be deployed in Rank 2 (found Rank {dep.TargetRank}).");
				}
			}
			else
			{
				list.Add($"Starting non-Warlord character '{definition.Name}' must be Level 1 or Level 2 (found Level {definition.Level}).");
			}
		}
		if (warlordCount != 1)
		{
			list.Add($"Starting army must include exactly one Warlord character (found {warlordCount}).");
		}
		if (level2Count != 2)
		{
			list.Add($"Starting army must include exactly 2 Level 2 characters in Rank 2 (found {level2Count}).");
		}
		if (level1Count != 3)
		{
			list.Add($"Starting army must include exactly 3 Level 1 characters in Rank 1 (found {level1Count}).");
		}
		HashSet<string> seenUniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item2 in list2)
		{
			CardDefinition item = item2.Item2;
			if (item.Traits.Any((string t) => t.Equals("Reserved", StringComparison.OrdinalIgnoreCase)) || item.RulesText.Contains("Reserved", StringComparison.OrdinalIgnoreCase))
			{
				list.Add($"Starting character '{item.Name}' has the Reserved keyword and cannot be deployed in starting army.");
			}
			if (item.Traits.Any((string t) => t.Equals("Unique", StringComparison.OrdinalIgnoreCase)))
			{
				if (!seenUniqueNames.Add(item.Name))
				{
					list.Add($"Starting army cannot contain duplicate Unique character '{item.Name}'.");
				}
			}
		}
		if (cardDefinition != null)
		{
			string effectiveFaction = cardDefinition.GetEffectiveFaction();
			foreach (var item2 in list2)
			{
				CardDefinition item = item2.Item2;
				if (!item.IsWarlord)
				{
					string effectiveFaction2 = item.GetEffectiveFaction();
					if (!effectiveFaction2.Equals(effectiveFaction, StringComparison.OrdinalIgnoreCase))
					{
						list.Add($"Starting character '{item.Name}' faction '{effectiveFaction2}' does not match Warlord faction '{effectiveFaction}'.");
					}
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	private ActionResult ValidateUseAbility(UseAbilityAction useAbility, GameState state)
	{
		List<string> list = new List<string>();
		if (useAbility.PlayerIndex < 0 || useAbility.PlayerIndex >= state.Players.Count)
		{
			return ActionResult.Failure($"Player index {useAbility.PlayerIndex} is out of range.");
		}
		CardInstance? cardInstance = null;
		int ownerPlayerIndex = -1;
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item2 in value)
				{
					if (item2.InstanceId == useAbility.SourceInstanceId)
					{
						cardInstance = item2;
						ownerPlayerIndex = player.PlayerIndex;
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
		CardInstance? bearerCard = null;
		if (!cardInstance.HasValue)
		{
			CardInstance? maybeItem = CardLocator.FindCard(state, useAbility.SourceInstanceId);
			if (maybeItem.HasValue)
			{
				int bearerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
				CardInstance? bearer = FindBoardCard(state, bearerId);
				if (bearer.HasValue)
				{
					bearerCard = bearer;
					cardInstance = maybeItem;
					ownerPlayerIndex = bearer.Value.OwnerPlayerIndex;
				}
			}
		}
		if (!cardInstance.HasValue)
		{
			return ActionResult.Failure("Source card not found on the board.");
		}
		if (bearerCard.HasValue && bearerCard.Value.ReadyState == ReadyState.Stunned)
		{
			list.Add("Character equipping this item is Stunned and cannot activate abilities.");
		}
		if (state.Priority.IsReactWindowOpen)
		{
			if (ownerPlayerIndex != useAbility.PlayerIndex)
			{
				list.Add("You do not control this card.");
			}
		}
		else if (ownerPlayerIndex != state.ActivePlayerIndex || useAbility.PlayerIndex != state.ActivePlayerIndex)
		{
			list.Add("You do not control this card.");
		}
		CardDefinition definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
		int abilityIdx = useAbility.AbilityIndex;
		if (useAbility.ChoiceIndex.HasValue && useAbility.ChoiceIndex.Value >= 0 && definition != null && useAbility.ChoiceIndex.Value < definition.Abilities.Count)
		{
			abilityIdx = useAbility.ChoiceIndex.Value;
		}
		if (definition == null || abilityIdx < 0 || abilityIdx >= definition.Abilities.Count)
		{
			return ActionResult.Failure("Invalid ability index.");
		}
		AbilityDefinition abilityDefinition = definition.Abilities[abilityIdx];
		string description = abilityDefinition.Description;
		if (description != null && description.Contains("wounded Dwarven Forges character", StringComparison.OrdinalIgnoreCase))
		{
			if (useAbility.TargetInstanceIds.Length == 0)
			{
				list.Add("Must target a wounded Dwarven Forges character.");
			}
			else
			{
				CardInstance? targetDwarf = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((targetDwarf = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!targetDwarf.HasValue || targetDwarf.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					list.Add("Target character must be controlled by you.");
				}
				else
				{
					CardDefinition definition2 = _cardDb.GetDefinition(targetDwarf.Value.DefinitionId);
					if (!IsDwarf(definition2) || !IsCharacterWounded(targetDwarf.Value, definition2, state, _cardDb))
					{
						list.Add("Target must be a wounded Dwarven character.");
					}
				}
			}
		}
		string description2 = abilityDefinition.Description;
		if (description2 != null && description2.Contains("non-stunned Dwarven Forges character in a front rank", StringComparison.OrdinalIgnoreCase))
		{
			if (useAbility.TargetInstanceIds.Length == 0)
			{
				list.Add("Must target a non-stunned Dwarven Forges character in a front rank.");
			}
			else
			{
				CardInstance? frontRankDwarf = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((frontRankDwarf = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!frontRankDwarf.HasValue || frontRankDwarf.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					list.Add("Target character must be controlled by you.");
				}
				else
				{
					if (!IsDwarf(_cardDb.GetDefinition(frontRankDwarf.Value.DefinitionId)))
					{
						list.Add("Target must be a Dwarven character.");
					}
					if (frontRankDwarf.Value.ReadyState == ReadyState.Stunned)
					{
						list.Add("Target character cannot be stunned.");
					}
					if (frontRankDwarf.Value.Rank != 1)
					{
						list.Add("Target character must be in a front rank (Rank 1).");
					}
				}
			}
		}
		ValidateMultiTargetSelection(state, useAbility, abilityDefinition, list);
		CardInstance? srcCardInst = CardLocator.FindCard(state, useAbility.SourceInstanceId);
		CardDefinition? srcCardDef = srcCardInst.HasValue ? _cardDb.GetDefinition(srcCardInst.Value.DefinitionId) : null;
		bool isHodsLegacyAbility = CardScriptRegistry.UsesLowerLevelFactionTargeting(srcCardDef);
		bool isHuntingStaffAbility = CardScriptRegistry.UsesAdjacentMoveForwardTargeting(srcCardDef);

		if (isHodsLegacyAbility || (abilityDefinition.Description?.Contains("Hod's Legacy", StringComparison.OrdinalIgnoreCase) ?? false) || ((abilityDefinition.Description?.Contains("lower level", StringComparison.OrdinalIgnoreCase) ?? false) && (abilityDefinition.Description?.Contains("same faction", StringComparison.OrdinalIgnoreCase) ?? false)))
		{
			if (useAbility.TargetInstanceIds.Length == 0)
			{
				list.Add("Must target a lower level character of the same faction.");
			}
			else
			{
				CardInstance? targetCard = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((targetCard = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!targetCard.HasValue || targetCard.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					list.Add("Target character must be an ally controlled by you.");
				}
				else
				{
					int performerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
					CardInstance? performerCard = FindBoardCard(state, performerId);
					int bearerLevel = performerCard.HasValue ? GetEffectiveLevel(state, performerCard.Value, _cardDb) : 0;
					int targetLevel = GetEffectiveLevel(state, targetCard.Value, _cardDb);
					if (targetLevel >= bearerLevel)
					{
						list.Add("Target must be lower level than the bearer.");
					}
					CardDefinition? bearerDef = performerCard.HasValue ? _cardDb.GetDefinition(performerCard.Value.DefinitionId) : null;
					CardDefinition? targetDef = _cardDb.GetDefinition(targetCard.Value.DefinitionId);
					if (!ShareFaction(bearerDef, targetDef))
					{
						list.Add("Target must share a faction with the bearer.");
					}
					bool readyAbil = abilityDefinition.Effects.OfType<ChangeReadyStateEffect>().Any((ChangeReadyStateEffect e) => e.TargetState == ReadyState.Ready);
					bool unstunAbil = abilityDefinition.Effects.OfType<ChangeReadyStateEffect>().Any((ChangeReadyStateEffect e) => e.OnlyIfStunned);
					if (readyAbil && targetCard.Value.ReadyState == ReadyState.Stunned)
					{
						list.Add("Target character cannot be stunned.");
					}
					if (unstunAbil && targetCard.Value.ReadyState != ReadyState.Stunned)
					{
						list.Add("Target character must be stunned.");
					}
				}
			}
		}

		if (isHuntingStaffAbility || (abilityDefinition.Description?.Contains("Hunting Staff", StringComparison.OrdinalIgnoreCase) ?? false) || ((abilityDefinition.Description?.Contains("adjacent character", StringComparison.OrdinalIgnoreCase) ?? false) && (abilityDefinition.Description?.Contains("moves forward one rank", StringComparison.OrdinalIgnoreCase) ?? false)))
		{
			if (useAbility.TargetInstanceIds.Length == 0)
			{
				list.Add("Must target an adjacent character.");
			}
			else
			{
				CardInstance? targetCard = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((targetCard = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!targetCard.HasValue)
				{
					list.Add("Target character not found.");
				}
				else
				{
					int performerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
					CardInstance? performerCard = FindBoardCard(state, performerId);
					if (performerCard.HasValue)
					{
						PlayerState playerState = state.Players[performerCard.Value.OwnerPlayerIndex];
						if (!BoardGeometry.AreAdjacent(playerState, performerCard.Value.InstanceId, targetCard.Value.InstanceId))
						{
							list.Add("Target must be adjacent to the bearer.");
						}
					}
				}
			}
		}
		if (state.Priority.IsReactWindowOpen)
		{
			string failureReason;
			if (!abilityDefinition.IsReact)
			{
				list.Add("Only React abilities can be used during a React window.");
			}
			else if (!IsReactTimingAndConditionMet(abilityDefinition, cardInstance.Value, state, useAbility.TargetInstanceIds, out failureReason))
			{
				list.Add(failureReason ?? "React timing condition is not satisfied.");
			}
		}
		else if ((abilityDefinition.Trigger == AbilityTrigger.Order || abilityDefinition.Trigger == AbilityTrigger.SpendOrder) && state.CurrentPhase != TurnPhase.Order && state.CurrentPhase != TurnPhase.Combat)
		{
			list.Add("Orders can only be used during the Order phase.");
		}
		if ((abilityDefinition.Trigger == AbilityTrigger.SpendOrder || abilityDefinition.Trigger == AbilityTrigger.SpendReact) && cardInstance.Value.ReadyState != ReadyState.Ready)
		{
			list.Add("Card must be ready to pay a spend cost.");
		}
		if (abilityDefinition.IsLimited)
		{
			string item = $"{useAbility.SourceInstanceId}_{useAbility.AbilityIndex}";
			if (state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains(item) || state.TurnState.UsedLimitedAbilityKeysThisTurn.Contains($"{useAbility.SourceInstanceId}_Limited") || cardInstance.Value.AbilitiesUsedThisTurn.Contains(useAbility.AbilityIndex))
			{
				list.Add("This limited ability has already been used this turn.");
			}
		}
		if (abilityDefinition.DiscardHandCost > 0)
		{
			PlayerState playerState = state.Players[useAbility.PlayerIndex];
			if (playerState.Hand.Count < abilityDefinition.DiscardHandCost)
			{
				list.Add($"Must have at least {abilityDefinition.DiscardHandCost} card(s) in hand to pay discard cost.");
			}
			else if (useAbility.DiscardCardInstanceId.HasValue && !playerState.Hand.Any((CardInstance c) => c.InstanceId == useAbility.DiscardCardInstanceId.Value))
			{
				list.Add($"Card {useAbility.DiscardCardInstanceId.Value} is not in hand to discard.");
			}
		}
		if (abilityDefinition.RequiresSpendAdjacentAlly)
		{
			int performerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
			(int, int, int)? tuple = FindCharacterPosition(state, performerId);
			if (!tuple.HasValue)
			{
				list.Add("Caster position not found.");
			}
			else
			{
				List<CardInstance> list2 = state.Players[tuple.Value.Item1].Ranks[tuple.Value.Item2];
				bool hasAdjacentCostAlly = false;
				if (useAbility.CostCharacterInstanceId.HasValue)
				{
					int costId = useAbility.CostCharacterInstanceId.Value;
					int costIndex = list2.FindIndex((CardInstance c) => c.InstanceId == costId);
					if (costIndex >= 0 && Math.Abs(costIndex - tuple.Value.Item3) == 1 && list2[costIndex].ReadyState == ReadyState.Ready)
					{
						hasAdjacentCostAlly = true;
					}
				}
				else
				{
					for (int allyIdx = 0; allyIdx < list2.Count; allyIdx++)
					{
						if (Math.Abs(allyIdx - tuple.Value.Item3) == 1 && list2[allyIdx].ReadyState == ReadyState.Ready)
						{
							hasAdjacentCostAlly = true;
							break;
						}
					}
				}
				if (!hasAdjacentCostAlly)
				{
					list.Add("Must have an adjacent ready ally to spend as cost.");
				}
			}
		}
		if (abilityDefinition.RequiresSpendAllyInRank)
		{
			int performerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
			(int, int, int)? tuple2 = FindCharacterPosition(state, performerId);
			if (!tuple2.HasValue)
			{
				list.Add("Caster position not found.");
			}
			else
			{
				List<CardInstance> list3 = state.Players[tuple2.Value.Item1].Ranks[tuple2.Value.Item2];
				bool hasRankCostAlly = false;
				if (useAbility.CostCharacterInstanceId.HasValue)
				{
					int costId2 = useAbility.CostCharacterInstanceId.Value;
					int costIndex = list3.FindIndex((CardInstance c) => c.InstanceId == costId2);
					if (costIndex >= 0 && list3[costIndex].InstanceId != performerId && list3[costIndex].ReadyState == ReadyState.Ready)
					{
						hasRankCostAlly = true;
					}
				}
				else
				{
					hasRankCostAlly = list3.Any((CardInstance c) => c.InstanceId != performerId && c.ReadyState == ReadyState.Ready);
				}
				if (!hasRankCostAlly)
				{
					list.Add("Must have a ready ally in this rank to spend as cost.");
				}
			}
		}
		if (abilityDefinition.BanishActionFromDiscardCost > 0 && state.Players[useAbility.PlayerIndex].DiscardPile.Count((CardInstance c) =>
		{
			CardDefinition definition3 = _cardDb.GetDefinition(c.DefinitionId);
			return definition3 != null && definition3.Type == CardType.Action;
		}) < abilityDefinition.BanishActionFromDiscardCost)
		{
			list.Add($"Must have at least {abilityDefinition.BanishActionFromDiscardCost} action card(s) in discard pile to banish.");
		}
		if (abilityDefinition.DiscardsOpponentTopDeck)
		{
			PlayerState opponentState = state.Players.FirstOrDefault((PlayerState pl) => pl.PlayerIndex != useAbility.PlayerIndex);
			if (opponentState == null || opponentState.Deck.Count == 0)
			{
				list.Add("Opponent has no cards in deck to discard.");
			}
		}
		if (abilityDefinition.StunSelfCost && cardInstance.Value.ReadyState == ReadyState.Stunned)
		{
			list.Add("Cannot pay stun cost because character is already stunned.");
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	private ActionResult ValidatePlayCard(PlayCardAction playCard, GameState state)
	{
		List<string> list = new List<string>();
		if (playCard.PlayerIndex < 0 || playCard.PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {playCard.PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		if (!state.Priority.IsReactWindowOpen)
		{
			if (playCard.PlayerIndex != state.ActivePlayerIndex)
			{
				list.Add($"It is not Player {playCard.PlayerIndex}'s turn. Active player is Player {state.ActivePlayerIndex}.");
			}
			if (state.CurrentPhase != TurnPhase.Order)
			{
				list.Add($"Cannot play cards during {state.CurrentPhase} phase. Playing cards is only allowed during the Order phase (Main phase).");
			}
		}
		PlayerState playerState = state.Players[playCard.PlayerIndex];
		CardInstance? cardInstance = null;
		foreach (CardInstance item in playerState.Hand)
		{
			if (item.InstanceId == playCard.CardInstanceId)
			{
				cardInstance = item;
				break;
			}
		}
		if (!cardInstance.HasValue)
		{
			list.Add($"Card instance {playCard.CardInstanceId} is not in Player {playCard.PlayerIndex}'s hand.");
			return ActionResult.Failure(list);
		}
		CardDefinition definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
		if ((object)definition == null)
		{
			list.Add("Card definition '" + cardInstance.Value.DefinitionId + "' not found in card database.");
			return ActionResult.Failure(list);
		}
		if (state.Priority.IsReactWindowOpen)
		{
			if (definition.Type != CardType.Action || !definition.Abilities.Any((AbilityDefinition a) => a.IsReact))
			{
				list.Add("Only Action cards with a React ability can be played during a React window.");
			}
			else
			{
				AbilityDefinition abilityDefinition = definition.Abilities.First((AbilityDefinition a) => a.IsReact);
				GameEvent topPendingEvent = GetTopPendingEvent(state);
				if (topPendingEvent == null)
				{
					if (abilityDefinition.Timing != ReactTiming.None)
					{
						list.Add("No matching trigger event on the stack.");
					}
				}
				else
				{
					switch (abilityDefinition.Timing)
					{
					case ReactTiming.BeforeStrikeRoll:
						if (!(topPendingEvent is DeclareAttackEvent))
						{
							list.Add("Card can only be played before a strike roll.");
						}
						break;
					case ReactTiming.AfterTargetedWithStrike:
						if (!(topPendingEvent is DeclareAttackEvent declareAttack))
						{
							list.Add("Card can only be played when a character is targeted with a strike.");
						}
						else
						{
							if (abilityDefinition.TargetScope == TargetScope.Self && playCard.PerformerInstanceId.HasValue && declareAttack.DefenderInstanceId != playCard.PerformerInstanceId.Value)
						{
							list.Add("Only the character targeted by the strike can perform this react.");
						}
							if (Regex.IsMatch(abilityDefinition.Description ?? "", "\\bmelee\\s+strike\\b", RegexOptions.IgnoreCase))
						{
							CardInstance? atkCard = FindBoardCard(state, declareAttack.AttackerInstanceId);
							CardDefinition? atkDef = atkCard.HasValue ? _cardDb.GetDefinition(atkCard.Value.DefinitionId) : null;
							if (atkDef != null && atkDef.Attack?.Strikes.Count > 0 && atkDef.Attack.Strikes.All((StrikeDescriptor s) => s.IsRanged))
							{
								list.Add("Dupe can only react to a melee strike, not a ranged strike.");
							}
						}
						if (Regex.IsMatch(abilityDefinition.Description ?? "", "\\bmove\\s+to\\s+a\\s+different\\s+position\\s+in\\s+this\\s+rank\\b", RegexOptions.IgnoreCase) && playCard.PerformerInstanceId.HasValue)
							{
								CardInstance? perf = FindBoardCard(state, playCard.PerformerInstanceId.Value);
								if (perf.HasValue && perf.Value.Rank.HasValue && state.Players[perf.Value.OwnerPlayerIndex].Ranks.TryGetValue(perf.Value.Rank.Value, out var rankCards) && rankCards.Count <= 1)
								{
									list.Add("Character must be in a rank with at least one other character to move to a different position in this rank.");
								}
							}
						}
						break;
					case ReactTiming.BeforeSuffersWounds:
					case ReactTiming.AfterSuffersWounds:
						if (!(topPendingEvent is DealDamageEvent))
						{
							list.Add("Card can only be played when a character suffers wounds.");
						}
						break;
					case ReactTiming.AfterMove:
						if (!(topPendingEvent is MarchEvent))
						{
							list.Add("Card can only be played after a character moves.");
						}
						break;
					case ReactTiming.AfterAttack:
						if (!(topPendingEvent is DeclareAttackEvent))
						{
							list.Add("Card can only be played after an attack.");
						}
						break;
					case ReactTiming.BeforeDies:
						if (!(topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent) || characterDefeatedEvent.OwnerPlayerIndex != playCard.PlayerIndex)
						{
							list.Add("Card can only be played before your character is killed.");
						}
						break;
					case ReactTiming.Kill:
						if (!(topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent2) || characterDefeatedEvent2.OwnerPlayerIndex == playCard.PlayerIndex)
						{
							list.Add("Card can only be played after a kill.");
						}
						break;
					case ReactTiming.AfterWizardCastsSpell:
						if (!(topPendingEvent is AbilityActivatedEvent))
						{
							list.Add("Card can only be played after your wizard casts a spell.");
						}
						break;
					case ReactTiming.None:
						if (abilityDefinition.Trigger == AbilityTrigger.EnterReact && !(topPendingEvent is PlayCardEvent))
						{
							list.Add("Card can only be played upon entering play.");
						}
						break;
					case ReactTiming.AfterStrikeHit:
						if (!(topPendingEvent is DealDamageEvent))
						{
							list.Add("Card can only be played after a strike hit.");
						}
						break;
					case ReactTiming.Enter:
						if (!(topPendingEvent is PlayCardEvent))
						{
							list.Add("Card can only be played upon entering play.");
						}
						break;
					}
				}
			}
		}
		bool isUniqueCard = definition.Traits.Any((string t) => t.Equals("Unique", StringComparison.OrdinalIgnoreCase)) || Regex.IsMatch(definition.RulesText ?? "", @"\bUnique\b", RegexOptions.IgnoreCase);
		if (isUniqueCard)
		{
			bool alreadyControlsSameName = false;
			foreach (List<CardInstance> rankList in playerState.Ranks.Values)
			{
				foreach (CardInstance c in rankList)
				{
					CardDefinition def = _cardDb.GetDefinition(c.DefinitionId);
					if (def != null && def.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControlsSameName = true;
						break;
					}
				}
				if (alreadyControlsSameName) break;
			}
			if (!alreadyControlsSameName)
			{
				foreach (CardInstance item in playerState.AttachedItems)
				{
					CardDefinition def = _cardDb.GetDefinition(item.DefinitionId);
					if (def != null && def.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControlsSameName = true;
						break;
					}
				}
			}
			if (alreadyControlsSameName)
			{
				if (definition.Type == CardType.Item)
				{
					list.Add($"Cannot equip Unique item '{definition.Name}': player already controls a card with the same name in play.");
				}
				else
				{
					list.Add($"Cannot play Unique character '{definition.Name}': player already controls a card with the same name in play.");
				}
			}
		}
		if (playCard.TargetRank.HasValue && definition.Type != CardType.Character && definition.Type != CardType.Token)
		{
			list.Add($"Card '{definition.Name}' is a {definition.Type}, not a Character. Only Characters can be deployed to a rank.");
		}
		switch (definition.Type)
		{
		case CardType.Character:
		case CardType.Token:
		{
			int requiredRank = ((definition.Level <= 0) ? 1 : definition.Level);
			int? targetRank = playCard.TargetRank;
			if ((!targetRank.HasValue || targetRank.GetValueOrDefault() < 1) ? true : false)
			{
				list.Add($"Target rank {playCard.TargetRank} is invalid. Playing a Character card requires a valid TargetRank (1-based).");
			}
			else if (playCard.TargetRank.Value != requiredRank)
			{
				list.Add($"Character '{definition.Name}' (Level {definition.Level}) must be deployed into Rank {requiredRank}.");
			}
			else
			{
				if (!(playCard.TargetRank > 1))
				{
					break;
				}
				if (!playerState.Ranks.TryGetValue(playCard.TargetRank.Value - 1, out List<CardInstance> prevRankCards) || prevRankCards.Count == 0)
				{
					list.Add($"Cannot deploy character to Rank {playCard.TargetRank}: Rank {playCard.TargetRank - 1} must be occupied first.");
					break;
				}
				int targetRankCharCount = (playerState.Ranks.TryGetValue(playCard.TargetRank.Value, out List<CardInstance> targetRankCards) ? targetRankCards.Count : 0);
				if (targetRankCharCount + 1 > prevRankCards.Count)
				{
					list.Add($"Cannot deploy character to Rank {playCard.TargetRank}: Rank {playCard.TargetRank} cannot have more characters ({targetRankCharCount + 1}) than Rank {playCard.TargetRank - 1} ({prevRankCards.Count}).");
				}
			}
			break;
		}
		case CardType.Item:
		{
			if (!playCard.TargetCharacterId.HasValue)
			{
				list.Add("Playing an Item card requires a TargetCharacterId.");
				break;
			}
			CardInstance? bearerTarget = FindBoardCard(state, playCard.TargetCharacterId.Value);
			if (!bearerTarget.HasValue)
			{
				list.Add($"Target character {playCard.TargetCharacterId.Value} not found on the board.");
				break;
			}
			if (bearerTarget.Value.OwnerPlayerIndex != playCard.PlayerIndex)
			{
				list.Add("Cannot attach items to an opponent's character.");
				break;
			}
			CardDefinition bearerDef = _cardDb.GetDefinition(bearerTarget.Value.DefinitionId);
			if (!(bearerDef != null))
			{
				break;
			}
			CardDefinition cardDefinition = bearerDef;
			bool isTinker = CardScriptRegistry.BearerIgnoresClassWhenEquipping(bearerDef);
			if (isTinker)
			{
				cardDefinition = bearerDef with
				{
					Level = bearerDef.Level + 1,
					Class = (definition.Class ?? bearerDef.Class)
				};
			}
			bool cardHasDualWield = HasDualWield(definition);
			bool bearerHasDualWield = HasDualWield(bearerDef) || bearerTarget.Value.AttachedItemIds.Any((int id) =>
			{
				CardInstance? equippedItemCard = CardLocator.FindCard(state, id);
				CardDefinition cardDefinition2 = (equippedItemCard.HasValue ? _cardDb.GetDefinition(equippedItemCard.Value.DefinitionId) : null);
				return HasDualWield(cardDefinition2);
			});
			if (cardHasDualWield | bearerHasDualWield)
			{
				cardDefinition = cardDefinition with
				{
					Traits = new List<string>(cardDefinition.Traits) { "Dual Wield" }
				};
			}
			ActionResult actionResult2 = PlayCardAction.ValidateItemEquip(definition, cardDefinition, bearerTarget.Value, state, _cardDb);
			if (actionResult2.IsSuccess)
			{
				break;
			}
			IEnumerable<string> enumerable = actionResult2.Errors.AsEnumerable();
			if (isTinker)
			{
				enumerable = enumerable.Where((string e) => !e.Contains("class", StringComparison.OrdinalIgnoreCase));
			}
			if (cardHasDualWield | bearerHasDualWield)
			{
				enumerable = enumerable.Where((string e) => !e.Contains("Weapon", StringComparison.OrdinalIgnoreCase) && !e.Contains("slot", StringComparison.OrdinalIgnoreCase));
			}
			list.AddRange(enumerable);
			break;
		}
		case CardType.Action:
			if (playCard.PerformerInstanceId.HasValue)
			{
				CardInstance? performerCard = FindBoardCard(state, playCard.PerformerInstanceId.Value);
				if (!performerCard.HasValue)
				{
					list.Add($"Performer character {playCard.PerformerInstanceId.Value} not found on the board.");
				}
				else if (performerCard.Value.OwnerPlayerIndex != playCard.PlayerIndex)
				{
					list.Add("Performer character does not belong to you.");
				}
				else
				{
					CardDefinition definition2 = _cardDb.GetDefinition(performerCard.Value.DefinitionId);
					if (definition2 != null)
					{
						ActionResult actionResult = PlayCardAction.ValidatePerformerRequirements(definition, definition2, performerCard.Value);
						if (!actionResult.IsSuccess)
						{
							list.AddRange(actionResult.Errors);
						}
						int chosenAbilityIndex = playCard.AbilityIndex ?? playCard.ChoiceIndex.GetValueOrDefault();
						if (chosenAbilityIndex >= 0 && chosenAbilityIndex < definition.Abilities.Count)
						{
							AbilityDefinition abilityChoice = definition.Abilities[chosenAbilityIndex];
							if (abilityChoice.RequiredPerformerClass != null && (!definition2.Class.HasValue || !definition2.Class.Value.ToString().Contains(abilityChoice.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)) && !definition2.Traits.Any((string t) => t.Contains(abilityChoice.RequiredPerformerClass, StringComparison.OrdinalIgnoreCase)))
							{
								list.Add($"Performer '{definition2.Name}' is not a {abilityChoice.RequiredPerformerClass} required by this ability.");
							}
						}
					}
				}
			}
			else if (playerState.Ranks.Values.Sum((List<CardInstance> r) => r.Count) > 0)
			{
				bool hasValidPerformer = false;
				foreach (List<CardInstance> rankCards in playerState.Ranks.Values)
				{
					foreach (CardInstance item2 in rankCards)
					{
						CardDefinition definition3 = _cardDb.GetDefinition(item2.DefinitionId);
						if (definition3 != null && PlayCardAction.ValidatePerformerRequirements(definition, definition3, item2).IsSuccess)
						{
							hasValidPerformer = true;
							break;
						}
					}
					if (hasValidPerformer)
					{
						break;
					}
				}
				if (!hasValidPerformer)
				{
					list.Add("No valid performer on the board meets the requirements to play action '" + definition.Name + "'.");
				}
			}
			if (!playCard.TargetCharacterId.HasValue)
			{
				break;
			}
			if (definition.Abilities.Any((AbilityDefinition a) => a.Effects.OfType<DestroyTargetItemEffect>().Any()) || Regex.IsMatch(definition.RulesText, "\\btarget\\s+an\\s+item\\b", RegexOptions.IgnoreCase))
			{
				CardInstance? targetAttachedItem = CardLocator.FindCard(state, playCard.TargetCharacterId.Value);
				if (!targetAttachedItem.HasValue || targetAttachedItem.Value.Zone != CardZone.Attached)
				{
					CardInstance? targetItemBearer = FindBoardCard(state, playCard.TargetCharacterId.Value);
					if (!targetItemBearer.HasValue || targetItemBearer.Value.AttachedItemIds.Count == 0)
					{
						list.Add($"Target item {playCard.TargetCharacterId.Value} for action card not found on the board.");
					}
				}
				ValidateDestroyItemTarget(state, playCard, definition, list);
			}
			else
			{
				CardInstance? targetChar = FindBoardCard(state, playCard.TargetCharacterId.Value);
				if (!targetChar.HasValue)
				{
					list.Add($"Target character {playCard.TargetCharacterId.Value} for action card not found on the board.");
				}
				else if ((CardScriptRegistry.RequiresSpentOrStunnedTarget(definition) || definition.Abilities.Any((AbilityDefinition a) => a.Effects.OfType<PerformStrikeEffect>().Any((PerformStrikeEffect s) => s.TargetSpentOnly || s.TargetSpentOrStunnedOnly)) || Regex.IsMatch(definition.RulesText, "\\b(?:spent\\s+or\\s+stunned|against\\s+a\\s+spent|target\\s+a\\s+spent)\\b", RegexOptions.IgnoreCase)) && targetChar.Value.ReadyState == ReadyState.Ready)
				{
					list.Add("'" + definition.Name + "' can only target a spent or stunned character.");
				}
				if (playCard.PerformerInstanceId.HasValue && targetChar.HasValue && !definition.Abilities.Any((AbilityDefinition a) => a.Effects.OfType<PerformStrikeEffect>().Any()) && Regex.IsMatch(definition.RulesText ?? "", @"\btarget\s+(?:a|an|your|opposing)?\s*(?:\w+\s+)?character\s+within\s+(?:two|2)\s+ranks\b", RegexOptions.IgnoreCase))
				{
					CardInstance? rangePerformer = FindBoardCard(state, playCard.PerformerInstanceId.Value);
					if (rangePerformer.HasValue && GetBoardRankDistance(rangePerformer.Value, targetChar.Value) > 2)
					{
						list.Add($"Target character {targetChar.Value.InstanceId} is not within two ranks of the performer.");
					}
				}
				int numAbil = playCard.AbilityIndex ?? playCard.ChoiceIndex.GetValueOrDefault();
				PerformStrikeEffect? strikeEff = null;
				if (numAbil >= 0 && numAbil < definition.Abilities.Count)
				{
					strikeEff = definition.Abilities[numAbil].Effects.OfType<PerformStrikeEffect>().FirstOrDefault();
				}
				else
				{
					strikeEff = definition.Abilities.SelectMany((AbilityDefinition a) => a.Effects).OfType<PerformStrikeEffect>().FirstOrDefault();
				}
				if (strikeEff != null && playCard.PerformerInstanceId.HasValue && targetChar.Value.OwnerPlayerIndex != playCard.PlayerIndex)
				{
					CardInstance? perf = FindBoardCard(state, playCard.PerformerInstanceId.Value);
					if (perf.HasValue && perf.Value.Rank.HasValue && targetChar.Value.Rank.HasValue)
					{
						if (IsTargetProtectedByStealth(state, perf.Value, targetChar.Value, strikeEff.IsRanged, _cardDb))
						{
							list.Add($"Target character {targetChar.Value.InstanceId} cannot be targeted due to Stealth.");
						}
						int crossDist = BoardGeometry.GetCrossBoardDistance(perf.Value.Rank.Value, targetChar.Value.Rank.Value);
						if (strikeEff.IsRanged)
						{
							int effectiveMax = strikeEff.HasReach ? Math.Max(strikeEff.MaxRange, 3) : strikeEff.MaxRange;
							if (crossDist > effectiveMax)
							{
								list.Add($"Target character {targetChar.Value.InstanceId} is out of range ({crossDist} > {effectiveMax}).");
							}
						}
						else
						{
							int maxMelee = (strikeEff.HasReach || CharacterHasReach(perf.Value, state, _cardDb)) ? 2 : 1;
							if (crossDist > maxMelee)
							{
								list.Add($"Target character {targetChar.Value.InstanceId} is out of melee range ({crossDist} > {maxMelee}).");
							}
						}
					}
				}
			}
			break;
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

}
