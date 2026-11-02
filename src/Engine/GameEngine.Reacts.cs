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
	private GameEvent? GetTopPendingEvent(GameState state)
	{
		if (state.PendingEvents.Count > 0)
		{
			return state.PendingEvents[0];
		}
		if (state == _state && _resolver.PendingCount > 0)
		{
			return _resolver.Peek();
		}
		return null;
	}

	public bool CanTriggerReact(GameState state, GameEvent triggerEvent, CardInstance sourceCard, AbilityDefinition ability, out string? failureReason)
	{
		state.PendingEvents.Insert(0, triggerEvent);
		try
		{
			return IsReactTimingAndConditionMet(ability, sourceCard, state, null, out failureReason);
		}
		finally
		{
			state.PendingEvents.RemoveAt(0);
		}
	}

	public bool IsReactTimingAndConditionMet(AbilityDefinition ability, CardInstance sourceCard, GameState state, int[]? targetInstanceIds, out string? failureReason)
	{
		failureReason = null;
		if (!ability.IsReact)
		{
			return true;
		}
		if (ability.Timing == ReactTiming.None && ability.TargetScope == TargetScope.Any)
		{
			return true;
		}
		GameEvent topPendingEvent = GetTopPendingEvent(state);
		if (topPendingEvent == null)
		{
			if (ability.Timing != ReactTiming.None)
			{
				failureReason = "No matching trigger event on the stack.";
				return false;
			}
			return true;
		}
		int performerId = ResolvePerformerCharacterId(state, sourceCard.InstanceId, _cardDb);
		CardInstance performerCard = (performerId == sourceCard.InstanceId)
			? sourceCard
			: (FindBoardCard(state, performerId) ?? CardLocator.FindCard(state, performerId) ?? sourceCard);
		int performerRank = performerCard.Rank ?? 1;
		switch (ability.Timing)
		{
		case ReactTiming.AfterMove:
			if (!(topPendingEvent is MarchEvent marchEvent))
			{
				failureReason = "Ability triggers after a move, but no move event is pending.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Self && marchEvent.CardInstanceId != performerId)
			{
				failureReason = "This character did not move.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Opposing)
			{
				CardInstance? movedCard = FindBoardCard(state, marchEvent.CardInstanceId);
				if (!movedCard.HasValue || movedCard.Value.OwnerPlayerIndex == performerCard.OwnerPlayerIndex)
				{
					failureReason = "Moving character is not an opponent.";
					return false;
				}
				if (ability.MaxRankDistance.HasValue)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(performerRank, marchEvent.DestinationRank);
					if (crossDist > ability.MaxRankDistance.Value)
					{
						failureReason = $"Moved character is not within {ability.MaxRankDistance.Value} ranks.";
						return false;
					}
				}
				if (targetInstanceIds != null && targetInstanceIds.Length > 0 && targetInstanceIds[0] != marchEvent.CardInstanceId)
				{
					failureReason = "Target must be the moving opposing character.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.Friendly)
			{
				CardInstance? movedCard = FindBoardCard(state, marchEvent.CardInstanceId);
				if (!movedCard.HasValue || movedCard.Value.OwnerPlayerIndex != performerCard.OwnerPlayerIndex)
				{
					failureReason = "Moving character is not friendly.";
					return false;
				}
			}
			break;
		case ReactTiming.BeforeSuffersWounds:
		case ReactTiming.AfterSuffersWounds:
			if (!(topPendingEvent is DealDamageEvent dealDamageEvent))
			{
				failureReason = "Ability triggers on suffering wounds, but no damage event is pending.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Self && dealDamageEvent.TargetInstanceId != performerId)
			{
				failureReason = "This character is not suffering wounds.";
				return false;
			}
			if (ability.TargetScope == TargetScope.SameRank)
			{
				CardInstance? damageTargetCard = FindBoardCard(state, dealDamageEvent.TargetInstanceId);
				if (!damageTargetCard.HasValue || (damageTargetCard.Value.Rank ?? 1) != performerRank || damageTargetCard.Value.OwnerPlayerIndex != performerCard.OwnerPlayerIndex)
				{
					failureReason = "The character suffering wounds is not in the same rank.";
					return false;
				}
			}
			break;
		case ReactTiming.BeforeStrikeRoll:
			if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent2))
			{
				failureReason = "Ability triggers before a strike roll, but no attack is pending.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Self && declareAttackEvent2.AttackerInstanceId != performerId)
			{
				failureReason = "This character is not performing the strike.";
				return false;
			}
			if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
			{
				CardInstance? attackerCard = FindBoardCard(state, declareAttackEvent2.AttackerInstanceId);
				if (!attackerCard.HasValue || Math.Abs((attackerCard.Value.Rank ?? 1) - performerRank) > ability.MaxRankDistance.Value)
				{
					failureReason = $"Attacker is not within {ability.MaxRankDistance.Value} ranks.";
					return false;
				}
			}
			break;
		case ReactTiming.AfterTargetedWithStrike:
			if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent4))
			{
				failureReason = "Ability triggers when targeted with a strike, but no attack is pending.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Self && declareAttackEvent4.DefenderInstanceId != performerId)
			{
				failureReason = "This character is not the target of the strike.";
				return false;
			}
			if (Regex.IsMatch(ability.Description ?? "", "\\bmove\\s+to\\s+a\\s+different\\s+position\\s+in\\s+this\\s+rank\\b", RegexOptions.IgnoreCase))
			{
				if (!state.Players[sourceCard.OwnerPlayerIndex].Ranks.TryGetValue(performerRank, out var rankCards) || rankCards.Count <= 1)
				{
					failureReason = "Character must be in a rank with at least one other character to move to a different position in this rank.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.OtherInSameRank)
			{
				CardInstance? defenderCard = FindBoardCard(state, declareAttackEvent4.DefenderInstanceId);
				if (!defenderCard.HasValue || (defenderCard.Value.Rank ?? 1) != performerRank || defenderCard.Value.InstanceId == sourceCard.InstanceId || defenderCard.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Defender is not another character in this rank.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
			{
				CardInstance? defenderCard = FindBoardCard(state, declareAttackEvent4.DefenderInstanceId);
				if (!defenderCard.HasValue || Math.Abs((defenderCard.Value.Rank ?? 1) - performerRank) > ability.MaxRankDistance.Value)
				{
					failureReason = $"Target is not within {ability.MaxRankDistance.Value} ranks.";
					return false;
				}
			}
			break;
		case ReactTiming.AfterAttack:
		{
			if (!(topPendingEvent is DeclareAttackEvent declareAttackEvent3))
			{
				failureReason = "Ability triggers after attack, but no attack is pending.";
				return false;
			}
			if (ability.TargetScope == TargetScope.Self && declareAttackEvent3.AttackerInstanceId != performerId)
			{
				failureReason = "This character did not perform the attack.";
				return false;
			}
			if (ability.Description?.Contains("targeting this character", StringComparison.OrdinalIgnoreCase) == true)
			{
				if (declareAttackEvent3.DefenderInstanceId != performerId)
				{
					failureReason = "This character was not targeted by the attack.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.Opposing)
			{
				CardInstance? atkCard = FindBoardCard(state, declareAttackEvent3.AttackerInstanceId);
				if (!atkCard.HasValue || atkCard.Value.OwnerPlayerIndex == performerCard.OwnerPlayerIndex)
				{
					failureReason = "Attacker is not an opponent.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.SameRank)
			{
				CardInstance? defenderCard = FindBoardCard(state, declareAttackEvent3.DefenderInstanceId);
				if (!defenderCard.HasValue || (defenderCard.Value.Rank ?? 1) != performerRank || defenderCard.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Target of the attack is not in this rank.";
					return false;
				}
			}
			if (ability.TargetScope != TargetScope.WithinRankDistance || !ability.MaxRankDistance.HasValue)
			{
				break;
			}
			CardInstance? attackerCard = FindBoardCard(state, declareAttackEvent3.AttackerInstanceId);
			if (!attackerCard.HasValue || Math.Abs((attackerCard.Value.Rank ?? 1) - performerRank) > ability.MaxRankDistance.Value)
			{
				failureReason = $"Attacker is not within {ability.MaxRankDistance.Value} ranks.";
				return false;
			}
			string description = ability.Description;
			if (description != null && description.Contains("ATK is lower than", StringComparison.OrdinalIgnoreCase))
			{
				CardDefinition definition = _cardDb.GetDefinition(attackerCard.Value.DefinitionId);
				int attackerAtk = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0) + GetContinuousPassiveBonuses(state, attackerCard.Value, new CombatContext
				{
					IsAttacking = true
				}, _cardDb).Atk;
				int effectiveLevel = GetEffectiveLevel(state, sourceCard, _cardDb);
				if (attackerAtk >= effectiveLevel)
				{
					failureReason = "Attacker's ATK is not lower than this character's level.";
					return false;
				}
			}
			break;
		}
		case ReactTiming.BeforeDies:
			if (topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent)
			{
				if (characterDefeatedEvent.BanishInsteadOfDiscard)
				{
					failureReason = "Character is being banished instead of destroyed; no death reacts trigger.";
					return false;
				}
				if (characterDefeatedEvent.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Dying character is not friendly.";
					return false;
				}
				int bearerOrSourceId = sourceCard.AttachedToInstanceId ?? sourceCard.InstanceId;
				if (ability.TargetScope == TargetScope.Self && characterDefeatedEvent.DefeatedInstanceId != bearerOrSourceId)
				{
					failureReason = "This character is not being killed.";
					return false;
				}
				if (ability.TargetScope == TargetScope.SameRank)
				{
					CardInstance? cardInstance = FindBoardCard(state, characterDefeatedEvent.DefeatedInstanceId);
					if (!cardInstance.HasValue || (cardInstance.Value.Rank ?? 1) != performerRank)
					{
						failureReason = "Dying character is not in the same rank.";
						return false;
					}
				}
				break;
			}
			failureReason = "Ability triggers before a character dies, but no death event is pending.";
			return false;
		case ReactTiming.Death:
		case ReactTiming.Kill:
			if (!(topPendingEvent is CharacterDefeatedEvent characterDefeatedEvent2))
			{
				failureReason = "Ability triggers on kill/death, but no death event is pending.";
				return false;
			}
			if (characterDefeatedEvent2.BanishInsteadOfDiscard)
			{
				failureReason = "Character was banished instead of destroyed; no death/kill reacts trigger.";
				return false;
			}
			if (ability.Timing == ReactTiming.Kill)
			{
				if (characterDefeatedEvent2.OwnerPlayerIndex == sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Defeated character is not an opposing character.";
					return false;
				}
				if (ability.TargetScope == TargetScope.Self && characterDefeatedEvent2.KillerInstanceId.HasValue && characterDefeatedEvent2.KillerInstanceId.Value != sourceCard.InstanceId)
				{
					failureReason = "This character did not kill the opposing character.";
					return false;
				}
			}
			break;
		case ReactTiming.AfterWizardCastsSpell:
		{
			if (!(topPendingEvent is AbilityActivatedEvent abilityActivatedEvent) || abilityActivatedEvent.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
			{
				failureReason = "Ability triggers after your wizard casts a spell.";
				return false;
			}
			CardInstance? casterCard = FindBoardCard(state, abilityActivatedEvent.SourceInstanceId);
			CardDefinition cardDefinition = (casterCard.HasValue ? _cardDb.GetDefinition(casterCard.Value.DefinitionId) : null);
			if (!(cardDefinition != null) || (cardDefinition.Class != CharacterClass.Wizard && !cardDefinition.Traits.Any((string t) => t.Contains("Wizard", StringComparison.OrdinalIgnoreCase))) || sourceCard.ReadyState == ReadyState.Ready)
			{
				failureReason = "Source is not a wizard casting a spell, or this character is already Ready.";
				return false;
			}
			break;
		}
		case ReactTiming.AfterStrikeHit:
			if (!(topPendingEvent is DealDamageEvent hitDamageEvent))
			{
				failureReason = "Ability triggers after a strike hit, but no damage event is pending.";
				return false;
			}
			if (hitDamageEvent.SourceInstanceId != sourceCard.InstanceId)
			{
				failureReason = "This character did not perform the strike that hit.";
				return false;
			}
			break;
		case ReactTiming.Enter:
			if (!(topPendingEvent is PlayCardEvent))
			{
				failureReason = "Ability triggers on enter, but no play card event is pending.";
				return false;
			}
			break;
		case ReactTiming.None:
			if (ability.Trigger == AbilityTrigger.EnterReact && !(topPendingEvent is PlayCardEvent))
			{
				failureReason = "Ability triggers on enter, but no play card event is pending.";
				return false;
			}
			break;
		}
		if (targetInstanceIds != null && targetInstanceIds.Length != 0)
		{
			foreach (int targetId in targetInstanceIds)
			{
				CardInstance? targetCard = FindBoardCard(state, targetId);
				if (!targetCard.HasValue)
				{
					failureReason = $"Target {targetId} not found.";
					return false;
				}
				int targetRank = targetCard.Value.Rank ?? 1;
				if (ability.TargetScope == TargetScope.SameRank && (targetRank != performerRank || targetCard.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex))
				{
					failureReason = "Target must be in the same rank as this card.";
					return false;
				}
				if (ability.TargetScope == TargetScope.OtherInSameRank && (targetRank != performerRank || targetCard.Value.InstanceId == sourceCard.InstanceId || targetCard.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex))
				{
					failureReason = "Target must be another character in the same rank.";
					return false;
				}
				if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue && Math.Abs(targetRank - performerRank) > ability.MaxRankDistance.Value)
				{
					failureReason = $"Target is not within {ability.MaxRankDistance.Value} ranks.";
					return false;
				}
				if (ability.TargetScope == TargetScope.Self && !ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect) ? true : false) && targetCard.Value.InstanceId != sourceCard.InstanceId)
				{
					failureReason = "Target must be this card.";
					return false;
				}
			}
		}
		return true;
	}

	private void TriggerCharacterEnterReacts(CardInstance card, int rank, int playerIndex)
	{
		CardDefinition definition = _cardDb.GetDefinition(card.DefinitionId);
		if (definition == null)
		{
			return;
		}
		if (definition.Name.Equals("Albrecht Novice", StringComparison.OrdinalIgnoreCase))
		{
			if (!_state.Players[playerIndex].Ranks.TryGetValue(rank, out List<CardInstance> rankCards))
			{
				return;
			}
			int cardIdx = rankCards.FindIndex((CardInstance c) => c.InstanceId == card.InstanceId);
			if (cardIdx >= 0)
			{
				if (cardIdx > 0)
				{
					AddTurnBuff(_state, rankCards[cardIdx - 1].InstanceId, "ATK", 2);
					AddTurnBuff(_state, rankCards[cardIdx - 1].InstanceId, "AC", 2);
				}
				if (cardIdx + 1 < rankCards.Count)
				{
					AddTurnBuff(_state, rankCards[cardIdx + 1].InstanceId, "ATK", 2);
					AddTurnBuff(_state, rankCards[cardIdx + 1].InstanceId, "AC", 2);
				}
			}
		}
		else if (definition.Name.Equals("Frameseer", StringComparison.OrdinalIgnoreCase))
		{
			AddTurnBuff(_state, card.InstanceId, "SURVEYED", 1);
		}
		else
		{
			if (!definition.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase) || !_state.Players[playerIndex].Ranks.TryGetValue(1, out List<CardInstance> rank1Cards))
			{
				return;
			}
			foreach (CardInstance ally in rank1Cards)
			{
				CardDefinition? definition2 = _cardDb.GetDefinition(ally.DefinitionId);
				int effectiveLevel = GetEffectiveLevel(_state, ally, _cardDb);
				if (IsFreeKingdoms(definition2) && effectiveLevel <= 2)
				{
					AddTurnBuff(_state, ally.InstanceId, "ATK", 2);
					AddTurnBuff(_state, ally.InstanceId, "AC", 2);
				}
			}
		}
	}

	private bool HasAnyLegalReact(GameState state)
	{
		bool isReactWindowOpen = state.Priority.IsReactWindowOpen;
		int activePlayerIndex = state.Priority.ActivePlayerIndex;
		try
		{
			state.Priority.IsReactWindowOpen = true;
			for (int i = 0; i < state.Players.Count; i++)
			{
				state.Priority.ActivePlayerIndex = i;
				IReadOnlyList<IPlayerAction> legalActions = GetLegalActions(state, i);
				for (int j = 0; j < legalActions.Count; j++)
				{
					IPlayerAction playerAction = legalActions[j];
					if (!(playerAction is PassAction))
					{
						return true;
					}
				}
			}
			return false;
		}
		finally
		{
			state.Priority.IsReactWindowOpen = isReactWindowOpen;
			state.Priority.ActivePlayerIndex = activePlayerIndex;
		}
	}

	private bool IsAbilityAlreadyPendingOnStack(GameState state, int sourceInstanceId, int abilityIndex)
	{
		if (state == _state)
		{
			foreach (GameEvent pendingEvent in _resolver.PendingEvents)
			{
				if (pendingEvent is AbilityActivatedEvent abilityActivatedEvent && abilityActivatedEvent.SourceInstanceId == sourceInstanceId && abilityActivatedEvent.AbilityIndex == abilityIndex)
				{
					return true;
				}
			}
		}
		foreach (GameEvent pendingEvent2 in state.PendingEvents)
		{
			if (pendingEvent2 is AbilityActivatedEvent abilityActivatedEvent2 && abilityActivatedEvent2.SourceInstanceId == sourceInstanceId && abilityActivatedEvent2.AbilityIndex == abilityIndex)
			{
				return true;
			}
		}
		return false;
	}

}
