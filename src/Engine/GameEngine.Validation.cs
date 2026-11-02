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
		if (action is MarchAction march)
		{
			return ValidateMarch(march, state);
		}
		if (action is PassAction)
		{
			return ValidatePassPriority(action, state);
		}
		if (action is UseAbilityAction useAbility)
		{
			return ValidateUseAbility(useAbility, state);
		}
		if (action is PlayCardAction playCard)
		{
			return ValidatePlayCard(playCard, state);
		}
		if (action is AttackAction attack)
		{
			return ValidateAttackAction(attack, state);
		}
		if (action is DeployStartingArmyAction deployArmy)
		{
			return ValidateDeployStartingArmy(deployArmy, state);
		}
		if (action is DiscardCardAction discardAction)
		{
			PlayerState p = state.Players[discardAction.PlayerIndex];
			if (!p.Hand.Any((CardInstance c) => c.InstanceId == discardAction.CardInstanceId))
			{
				return ActionResult.Failure("Card is not in hand.");
			}
			return ActionResult.Success();
		}
		return ActionResult.Failure("Unknown action type: " + action.GetType().Name);
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
			bool flag = false;
			foreach (CardInstance item in playerState.Ranks[march.SourceRank])
			{
				if (item.InstanceId == march.CardInstanceId)
				{
					if (item.ReadyState != ReadyState.Ready)
					{
						list.Add("Cannot march a spent or stunned character.");
					}
					flag = true;
					break;
				}
			}
			if (!flag)
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
			int num2 = (from kvp in dictionary
				where kvp.Value > 0
				select kvp.Key).DefaultIfEmpty(0).Max();
			if (dictionary.GetValueOrDefault(1, 0) == 0 && num2 >= 2)
			{
				list.Add($"Cannot march to Rank {march.DestinationRank}: cannot leave Rank 1 empty when Rank 2 or higher exists.");
			}
			else
			{
				for (int num3 = 2; num3 <= num2; num3++)
				{
					int valueOrDefault = dictionary.GetValueOrDefault(num3 - 1, 0);
					int valueOrDefault2 = dictionary.GetValueOrDefault(num3, 0);
					if (valueOrDefault2 > valueOrDefault)
					{
						list.Add($"Cannot march to Rank {march.DestinationRank}: formation rule violated (Rank {num3} would have {valueOrDefault2} characters, exceeding Rank {num3 - 1} with {valueOrDefault}).");
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
		int? num = null;
		int key;
		List<CardInstance> value;
		foreach (KeyValuePair<int, List<CardInstance>> rank in state.Players[attack.PlayerIndex].Ranks)
		{
			rank.Deconstruct(out key, out value);
			int value2 = key;
			foreach (CardInstance item in value)
			{
				if (item.InstanceId == attack.AttackerInstanceId)
				{
					cardInstance = item;
					num = value2;
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
		CardInstance? cardInstance2 = null;
		int? num2 = null;
		int num3 = -1;
		foreach (PlayerState player in state.Players)
		{
			foreach (KeyValuePair<int, List<CardInstance>> rank2 in player.Ranks)
			{
				rank2.Deconstruct(out key, out value);
				int value3 = key;
				foreach (CardInstance item2 in value)
				{
					if (item2.InstanceId == attack.TargetInstanceId)
					{
						cardInstance2 = item2;
						num2 = value3;
						num3 = player.PlayerIndex;
						break;
					}
				}
				if (cardInstance2.HasValue)
				{
					break;
				}
			}
			if (cardInstance2.HasValue)
			{
				break;
			}
		}
		if (!cardInstance2.HasValue)
		{
			list.Add($"Target {attack.TargetInstanceId} not found on the board.");
		}
		else if (num3 == attack.PlayerIndex)
		{
			list.Add("Cannot attack your own character.");
		}
		if (cardInstance.HasValue && cardInstance2.HasValue && num.HasValue && num2.HasValue)
		{
			CardDefinition? definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
			bool num4 = (object)definition != null && definition.Attack?.Strikes.Any((StrikeDescriptor s) => s.IsRanged) == true;
			if (IsTargetProtectedByStealth(state, cardInstance.Value, cardInstance2.Value, num4, _cardDb))
			{
				list.Add($"Cannot target '{_cardDb.GetDefinition(cardInstance2.Value.DefinitionId)?.Name ?? cardInstance2.Value.DefinitionId}' due to Stealth.");
			}
			int rankDistance = BoardGeometry.GetCrossBoardDistance(num.Value, num2.Value);
			if (num4)
			{
				if (!BoardGeometry.IsRangedValid(num.Value, num2.Value, 2))
				{
					list.Add($"Ranged attacks reach 2 ranks away (distance is {rankDistance}). Attacker is at Rank {num.Value}, Target is at Rank {num2.Value}.");
				}
			}
			else
			{
				int num5 = (CharacterHasReach(cardInstance.Value, state, _cardDb) ? 3 : 2);
				if (num.Value + num2.Value > num5)
				{
					list.Add($"Melee attacks require rank sum <= {num5}. Attacker is at Rank {num.Value}, Target is at Rank {num2.Value}.");
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
		int num = 0;
		int num2 = 0;
		int num3 = 0;
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
				num++;
				cardDefinition = definition;
				if (dep.TargetRank != 3)
				{
					list.Add($"Warlord '{definition.Name}' must be deployed in Rank 3 (found Rank {dep.TargetRank}).");
				}
			}
			else if (definition.Level == 1)
			{
				num2++;
				if (dep.TargetRank != 1)
				{
					list.Add($"Level 1 character '{definition.Name}' must be deployed in Rank 1 (found Rank {dep.TargetRank}).");
				}
			}
			else if (definition.Level == 2)
			{
				num3++;
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
		if (num != 1)
		{
			list.Add($"Starting army must include exactly one Warlord character (found {num}).");
		}
		if (num3 != 2)
		{
			list.Add($"Starting army must include exactly 2 Level 2 characters in Rank 2 (found {num3}).");
		}
		if (num2 != 3)
		{
			list.Add($"Starting army must include exactly 3 Level 1 characters in Rank 1 (found {num2}).");
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
		int num = -1;
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item2 in value)
				{
					if (item2.InstanceId == useAbility.SourceInstanceId)
					{
						cardInstance = item2;
						num = player.PlayerIndex;
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
		CardInstance? cardInstance2 = null;
		if (!cardInstance.HasValue)
		{
			CardInstance? maybeItem = CardLocator.FindCard(state, useAbility.SourceInstanceId);
			if (maybeItem.HasValue)
			{
				int bearerId = ResolvePerformerCharacterId(state, useAbility.SourceInstanceId, _cardDb);
				CardInstance? bearer = FindBoardCard(state, bearerId);
				if (bearer.HasValue)
				{
					cardInstance2 = bearer;
					cardInstance = maybeItem;
					num = bearer.Value.OwnerPlayerIndex;
				}
			}
		}
		if (!cardInstance.HasValue)
		{
			return ActionResult.Failure("Source card not found on the board.");
		}
		if (cardInstance2.HasValue && cardInstance2.Value.ReadyState == ReadyState.Stunned)
		{
			list.Add("Character equipping this item is Stunned and cannot activate abilities.");
		}
		if (state.Priority.IsReactWindowOpen)
		{
			if (num != useAbility.PlayerIndex)
			{
				list.Add("You do not control this card.");
			}
		}
		else if (num != state.ActivePlayerIndex || useAbility.PlayerIndex != state.ActivePlayerIndex)
		{
			list.Add("You do not control this card.");
		}
		CardDefinition definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
		int num2 = useAbility.AbilityIndex;
		if (useAbility.ChoiceIndex.HasValue && useAbility.ChoiceIndex.Value >= 0 && definition != null && useAbility.ChoiceIndex.Value < definition.Abilities.Count)
		{
			num2 = useAbility.ChoiceIndex.Value;
		}
		if (definition == null || num2 < 0 || num2 >= definition.Abilities.Count)
		{
			return ActionResult.Failure("Invalid ability index.");
		}
		AbilityDefinition abilityDefinition = definition.Abilities[num2];
		string description = abilityDefinition.Description;
		if (description != null && description.Contains("wounded Dwarven Forges character", StringComparison.OrdinalIgnoreCase))
		{
			if (useAbility.TargetInstanceIds.Length == 0)
			{
				list.Add("Must target a wounded Dwarven Forges character.");
			}
			else
			{
				CardInstance? cardInstance3 = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((cardInstance3 = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!cardInstance3.HasValue || cardInstance3.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					list.Add("Target character must be controlled by you.");
				}
				else
				{
					CardDefinition definition2 = _cardDb.GetDefinition(cardInstance3.Value.DefinitionId);
					if (!IsDwarf(definition2) || !IsCharacterWounded(cardInstance3.Value, definition2, state, _cardDb))
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
				CardInstance? cardInstance4 = null;
				for (int targetIdx = 0; targetIdx < useAbility.TargetInstanceIds.Length && ((cardInstance4 = CardLocator.FindCard(state, useAbility.TargetInstanceIds[targetIdx])).HasValue || true); targetIdx++)
				if (!cardInstance4.HasValue || cardInstance4.Value.OwnerPlayerIndex != useAbility.PlayerIndex)
				{
					list.Add("Target character must be controlled by you.");
				}
				else
				{
					if (!IsDwarf(_cardDb.GetDefinition(cardInstance4.Value.DefinitionId)))
					{
						list.Add("Target must be a Dwarven character.");
					}
					if (cardInstance4.Value.ReadyState == ReadyState.Stunned)
					{
						list.Add("Target character cannot be stunned.");
					}
					if (cardInstance4.Value.Rank != 1)
					{
						list.Add("Target character must be in a front rank (Rank 1).");
					}
				}
			}
		}
		ValidateMultiTargetSelection(state, useAbility, abilityDefinition, list);
		CardInstance? srcCardInst = CardLocator.FindCard(state, useAbility.SourceInstanceId);
		CardDefinition? srcCardDef = srcCardInst.HasValue ? _cardDb.GetDefinition(srcCardInst.Value.DefinitionId) : null;
		bool isHodsLegacyAbility = srcCardDef != null && srcCardDef.Name.Equals("Hod's Legacy", StringComparison.OrdinalIgnoreCase);
		bool isHuntingStaffAbility = srcCardDef != null && srcCardDef.Name.Equals("Hunting Staff", StringComparison.OrdinalIgnoreCase);

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
					CardInstance? bearerCard = FindBoardCard(state, performerId);
					int bearerLevel = bearerCard.HasValue ? GetEffectiveLevel(state, bearerCard.Value, _cardDb) : 0;
					int targetLevel = GetEffectiveLevel(state, targetCard.Value, _cardDb);
					if (targetLevel >= bearerLevel)
					{
						list.Add("Target must be lower level than the bearer.");
					}
					CardDefinition? bearerDef = bearerCard.HasValue ? _cardDb.GetDefinition(bearerCard.Value.DefinitionId) : null;
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
					CardInstance? bearerCard = FindBoardCard(state, performerId);
					if (bearerCard.HasValue)
					{
						PlayerState playerState = state.Players[bearerCard.Value.OwnerPlayerIndex];
						if (!BoardGeometry.AreAdjacent(playerState, bearerCard.Value.InstanceId, targetCard.Value.InstanceId))
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
				bool flag = false;
				if (useAbility.CostCharacterInstanceId.HasValue)
				{
					int costId = useAbility.CostCharacterInstanceId.Value;
					int num3 = list2.FindIndex((CardInstance c) => c.InstanceId == costId);
					if (num3 >= 0 && Math.Abs(num3 - tuple.Value.Item3) == 1 && list2[num3].ReadyState == ReadyState.Ready)
					{
						flag = true;
					}
				}
				else
				{
					for (int num4 = 0; num4 < list2.Count; num4++)
					{
						if (Math.Abs(num4 - tuple.Value.Item3) == 1 && list2[num4].ReadyState == ReadyState.Ready)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
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
				bool flag2 = false;
				if (useAbility.CostCharacterInstanceId.HasValue)
				{
					int costId2 = useAbility.CostCharacterInstanceId.Value;
					int num5 = list3.FindIndex((CardInstance c) => c.InstanceId == costId2);
					if (num5 >= 0 && list3[num5].InstanceId != performerId && list3[num5].ReadyState == ReadyState.Ready)
					{
						flag2 = true;
					}
				}
				else
				{
					flag2 = list3.Any((CardInstance c) => c.InstanceId != performerId && c.ReadyState == ReadyState.Ready);
				}
				if (!flag2)
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
			PlayerState playerState2 = state.Players.FirstOrDefault((PlayerState pl) => pl.PlayerIndex != useAbility.PlayerIndex);
			if (playerState2 == null || playerState2.Deck.Count == 0)
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
			int num2 = ((definition.Level <= 0) ? 1 : definition.Level);
			int? targetRank = playCard.TargetRank;
			if ((!targetRank.HasValue || targetRank.GetValueOrDefault() < 1) ? true : false)
			{
				list.Add($"Target rank {playCard.TargetRank} is invalid. Playing a Character card requires a valid TargetRank (1-based).");
			}
			else if (playCard.TargetRank.Value != num2)
			{
				list.Add($"Character '{definition.Name}' (Level {definition.Level}) must be deployed into Rank {num2}.");
			}
			else
			{
				if (!(playCard.TargetRank > 1))
				{
					break;
				}
				if (!playerState.Ranks.TryGetValue(playCard.TargetRank.Value - 1, out List<CardInstance> value) || value.Count == 0)
				{
					list.Add($"Cannot deploy character to Rank {playCard.TargetRank}: Rank {playCard.TargetRank - 1} must be occupied first.");
					break;
				}
				int num3 = (playerState.Ranks.TryGetValue(playCard.TargetRank.Value, out List<CardInstance> value2) ? value2.Count : 0);
				if (num3 + 1 > value.Count)
				{
					list.Add($"Cannot deploy character to Rank {playCard.TargetRank}: Rank {playCard.TargetRank} cannot have more characters ({num3 + 1}) than Rank {playCard.TargetRank - 1} ({value.Count}).");
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
			CardInstance? cardInstance6 = FindBoardCard(state, playCard.TargetCharacterId.Value);
			if (!cardInstance6.HasValue)
			{
				list.Add($"Target character {playCard.TargetCharacterId.Value} not found on the board.");
				break;
			}
			if (cardInstance6.Value.OwnerPlayerIndex != playCard.PlayerIndex)
			{
				list.Add("Cannot attach items to an opponent's character.");
				break;
			}
			CardDefinition definition4 = _cardDb.GetDefinition(cardInstance6.Value.DefinitionId);
			if (!(definition4 != null))
			{
				break;
			}
			CardDefinition cardDefinition = definition4;
			bool flag2 = definition4.Name.Equals("Tinker", StringComparison.OrdinalIgnoreCase);
			if (flag2)
			{
				cardDefinition = definition4 with
				{
					Level = definition4.Level + 1,
					Class = (definition.Class ?? definition4.Class)
				};
			}
			bool flag3 = HasDualWield(definition);
			bool flag4 = HasDualWield(definition4) || cardInstance6.Value.AttachedItemIds.Any((int id) =>
			{
				CardInstance? cardInstance7 = CardLocator.FindCard(state, id);
				CardDefinition cardDefinition2 = (cardInstance7.HasValue ? _cardDb.GetDefinition(cardInstance7.Value.DefinitionId) : null);
				return HasDualWield(cardDefinition2);
			});
			if (flag3 | flag4)
			{
				cardDefinition = cardDefinition with
				{
					Traits = new List<string>(cardDefinition.Traits) { "Dual Wield" }
				};
			}
			ActionResult actionResult2 = PlayCardAction.ValidateItemEquip(definition, cardDefinition, cardInstance6.Value, state, _cardDb);
			if (actionResult2.IsSuccess)
			{
				break;
			}
			IEnumerable<string> enumerable = actionResult2.Errors.AsEnumerable();
			if (flag2)
			{
				enumerable = enumerable.Where((string e) => !e.Contains("class", StringComparison.OrdinalIgnoreCase));
			}
			if (flag3 | flag4)
			{
				enumerable = enumerable.Where((string e) => !e.Contains("Weapon", StringComparison.OrdinalIgnoreCase) && !e.Contains("slot", StringComparison.OrdinalIgnoreCase));
			}
			list.AddRange(enumerable);
			break;
		}
		case CardType.Action:
			if (playCard.PerformerInstanceId.HasValue)
			{
				CardInstance? cardInstance2 = FindBoardCard(state, playCard.PerformerInstanceId.Value);
				if (!cardInstance2.HasValue)
				{
					list.Add($"Performer character {playCard.PerformerInstanceId.Value} not found on the board.");
				}
				else if (cardInstance2.Value.OwnerPlayerIndex != playCard.PlayerIndex)
				{
					list.Add("Performer character does not belong to you.");
				}
				else
				{
					CardDefinition definition2 = _cardDb.GetDefinition(cardInstance2.Value.DefinitionId);
					if (definition2 != null)
					{
						ActionResult actionResult = PlayCardAction.ValidatePerformerRequirements(definition, definition2, cardInstance2.Value);
						if (!actionResult.IsSuccess)
						{
							list.AddRange(actionResult.Errors);
						}
						int num = playCard.AbilityIndex ?? playCard.ChoiceIndex.GetValueOrDefault();
						if (num >= 0 && num < definition.Abilities.Count)
						{
							AbilityDefinition abilityChoice = definition.Abilities[num];
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
				bool flag = false;
				foreach (List<CardInstance> value3 in playerState.Ranks.Values)
				{
					foreach (CardInstance item2 in value3)
					{
						CardDefinition definition3 = _cardDb.GetDefinition(item2.DefinitionId);
						if (definition3 != null && PlayCardAction.ValidatePerformerRequirements(definition, definition3, item2).IsSuccess)
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
				if (!flag)
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
				CardInstance? cardInstance3 = CardLocator.FindCard(state, playCard.TargetCharacterId.Value);
				if (!cardInstance3.HasValue || cardInstance3.Value.Zone != CardZone.Attached)
				{
					CardInstance? cardInstance4 = FindBoardCard(state, playCard.TargetCharacterId.Value);
					if (!cardInstance4.HasValue || cardInstance4.Value.AttachedItemIds.Count == 0)
					{
						list.Add($"Target item {playCard.TargetCharacterId.Value} for action card not found on the board.");
					}
				}
				ValidateDestroyItemTarget(state, playCard, definition, list);
				if (true)
				{
					if (false)
					{
					}
				}
			}
			else
			{
				CardInstance? cardInstance5 = FindBoardCard(state, playCard.TargetCharacterId.Value);
				if (!cardInstance5.HasValue)
				{
					list.Add($"Target character {playCard.TargetCharacterId.Value} for action card not found on the board.");
				}
				else if ((definition.Name.Equals("Assassin's Strike", StringComparison.OrdinalIgnoreCase) || definition.Abilities.Any((AbilityDefinition a) => a.Effects.OfType<PerformStrikeEffect>().Any((PerformStrikeEffect s) => s.TargetSpentOnly || s.TargetSpentOrStunnedOnly)) || Regex.IsMatch(definition.RulesText, "\\b(?:spent\\s+or\\s+stunned|against\\s+a\\s+spent|target\\s+a\\s+spent)\\b", RegexOptions.IgnoreCase)) && cardInstance5.Value.ReadyState == ReadyState.Ready)
				{
					list.Add("'" + definition.Name + "' can only target a spent or stunned character.");
				}
				if (playCard.PerformerInstanceId.HasValue && cardInstance5.HasValue && !definition.Abilities.Any((AbilityDefinition a) => a.Effects.OfType<PerformStrikeEffect>().Any()) && Regex.IsMatch(definition.RulesText ?? "", @"\btarget\s+(?:a|an|your|opposing)?\s*(?:\w+\s+)?character\s+within\s+(?:two|2)\s+ranks\b", RegexOptions.IgnoreCase))
				{
					CardInstance? rangePerformer = FindBoardCard(state, playCard.PerformerInstanceId.Value);
					if (rangePerformer.HasValue && GetBoardRankDistance(rangePerformer.Value, cardInstance5.Value) > 2)
					{
						list.Add($"Target character {cardInstance5.Value.InstanceId} is not within two ranks of the performer.");
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
				if (strikeEff != null && playCard.PerformerInstanceId.HasValue && cardInstance5.Value.OwnerPlayerIndex != playCard.PlayerIndex)
				{
					CardInstance? perf = FindBoardCard(state, playCard.PerformerInstanceId.Value);
					if (perf.HasValue && perf.Value.Rank.HasValue && cardInstance5.Value.Rank.HasValue)
					{
						if (IsTargetProtectedByStealth(state, perf.Value, cardInstance5.Value, strikeEff.IsRanged, _cardDb))
						{
							list.Add($"Target character {cardInstance5.Value.InstanceId} cannot be targeted due to Stealth.");
						}
						int crossDist = BoardGeometry.GetCrossBoardDistance(perf.Value.Rank.Value, cardInstance5.Value.Rank.Value);
						if (strikeEff.IsRanged)
						{
							int effectiveMax = strikeEff.HasReach ? Math.Max(strikeEff.MaxRange, 3) : strikeEff.MaxRange;
							if (crossDist > effectiveMax)
							{
								list.Add($"Target character {cardInstance5.Value.InstanceId} is out of range ({crossDist} > {effectiveMax}).");
							}
						}
						else
						{
							int maxMelee = (strikeEff.HasReach || CharacterHasReach(perf.Value, state, _cardDb)) ? 2 : 1;
							if (crossDist > maxMelee)
							{
								list.Add($"Target character {cardInstance5.Value.InstanceId} is out of melee range ({crossDist} > {maxMelee}).");
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
