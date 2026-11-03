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
	private sealed class AttackResolutionHandler(GameEngine engine) : IPostResolutionHandler
	{
		public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
		{
			DeclareAttackEvent declareAttack = resolvedEvent as DeclareAttackEvent;
			if ((object)declareAttack == null)
			{
				yield break;
			}
			CardInstance? attackerCard = null;
			foreach (PlayerState player in state.Players)
			{
				foreach (List<CardInstance> rankCards in player.Ranks.Values)
				{
					foreach (CardInstance item2 in rankCards)
					{
						if (item2.InstanceId == declareAttack.AttackerInstanceId)
						{
							attackerCard = item2;
							break;
						}
					}
					if (attackerCard.HasValue)
					{
						break;
					}
				}
				if (attackerCard.HasValue)
				{
					break;
				}
			}
			CardInstance? defenderCard = null;
			foreach (PlayerState player2 in state.Players)
			{
				foreach (List<CardInstance> rankCards in player2.Ranks.Values)
				{
					foreach (CardInstance item3 in rankCards)
					{
						if (item3.InstanceId == declareAttack.DefenderInstanceId)
						{
							defenderCard = item3;
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
			if (!attackerCard.HasValue || !defenderCard.HasValue || attackerCard.Value.ReadyState == ReadyState.Stunned)
			{
				yield break;
			}
			PlayerState playerState = state.Players[defenderCard.Value.OwnerPlayerIndex];
			CardDefinition def = engine._cardDb.GetDefinition(attackerCard.Value.DefinitionId);
			bool attackerHasTrueStrike = HasTurnBuff(state, attackerCard.Value.InstanceId, "TRUE_STRIKE") ||
				GetContinuousPassiveBonuses(state, attackerCard.Value, new CombatContext { IsAttacking = true, OpponentCard = defenderCard }, engine._cardDb).TrueStrike ||
				(def != null && (def.Traits.Any((string t) => t.Equals("True Strike", StringComparison.OrdinalIgnoreCase)) || def.RulesText.Contains("True Strike", StringComparison.OrdinalIgnoreCase)));
			if (!attackerHasTrueStrike && defenderCard.Value.Rank.HasValue && playerState.Ranks.TryGetValue(defenderCard.Value.Rank.Value, out List<CardInstance> value))
			{
				foreach (CardInstance item4 in value)
				{
					if (item4.InstanceId != defenderCard.Value.InstanceId && HasTurnBuff(state, item4.InstanceId, "REDIRECT_TO_ME"))
					{
						defenderCard = item4;
						declareAttack = declareAttack with
						{
							DefenderInstanceId = item4.InstanceId
						};
						break;
					}
				}
			}
			if (!attackerHasTrueStrike && HasTurnBuff(state, defenderCard.Value.InstanceId, "DUPE_ACTIVE"))
			{
				int baseDefSkill = (engine._cardDb.GetDefinition(defenderCard.Value.DefinitionId)?.Skill).GetValueOrDefault();
				int defSkill = (defenderCard.Value.CurrentSkill > 0 ? defenderCard.Value.CurrentSkill : baseDefSkill) + GetTurnBuffValue(state, defenderCard.Value.InstanceId, "SKILL");
				int targetDC = 8 + defSkill;
				int baseAtkSkill = (engine._cardDb.GetDefinition(attackerCard.Value.DefinitionId)?.Skill).GetValueOrDefault();
				int atkSkill = (attackerCard.Value.CurrentSkill > 0 ? attackerCard.Value.CurrentSkill : baseAtkSkill) + GetTurnBuffValue(state, attackerCard.Value.InstanceId, "SKILL");
				if (engine._diceRoller(1, 20) + atkSkill < targetDC)
				{
					AddTurnBuff(state, attackerCard.Value.InstanceId, "NO_MELEE_STRIKES", 1);
				}
				yield break;
			}
			List<StrikeDescriptor> strikes = new List<StrikeDescriptor>();
			CardDefinition cardDefinition = def;
			if ((object)cardDefinition != null && cardDefinition.Attack?.Strikes.Count > 0)
			{
				strikes.AddRange(def.Attack.Strikes);
			}
			if (strikes.Count == 0)
			{
				strikes.Add(new StrikeDescriptor
				{
					Modifier = 0,
					IsRanged = false
				});
			}
			PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, attackerCard.Value, new CombatContext
			{
				IsAttacking = true,
				OpponentCard = defenderCard
			}, engine._cardDb);
			if (continuousPassiveBonuses.AdditionalStrikes.Count > 0)
			{
				strikes.AddRange(continuousPassiveBonuses.AdditionalStrikes);
			}
			bool hasAdvantage = HasTurnBuff(state, declareAttack.AttackerInstanceId, "ADVANTAGE");
			bool flagDis = HasTurnBuff(state, declareAttack.AttackerInstanceId, "DISADVANTAGE");
			DiceRoller diceRoller = engine._diceRoller;
			if (hasAdvantage && !flagDis)
			{
				diceRoller = (int count, int sides) =>
				{
					if (count == 1 && sides == 20)
					{
						int val = engine._diceRoller(1, 20);
						int val2 = engine._diceRoller(1, 20);
						return Math.Max(val, val2);
					}
					return engine._diceRoller(count, sides);
				};
			}
			else if (flagDis && !hasAdvantage)
			{
				diceRoller = (int count, int sides) =>
				{
					if (count == 1 && sides == 20)
					{
						int val = engine._diceRoller(1, 20);
						int val2 = engine._diceRoller(1, 20);
						return Math.Min(val, val2);
					}
					return engine._diceRoller(count, sides);
				};
			}
			int attackerItemBonus = 0;
			foreach (int attachedItemId in attackerCard.Value.AttachedItemIds)
			{
				CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
				if (cardInstance.HasValue)
				{
					CardDefinition definition = engine._cardDb.GetDefinition(cardInstance.Value.DefinitionId);
					attackerItemBonus += GetItemPassiveStats(definition).Atk;
				}
			}
			Dictionary<int, int> strikeWounds = new Dictionary<int, int>();
			int previousDefId = declareAttack.DefenderInstanceId;
			for (int strikeIdx = 0; strikeIdx < strikes.Count; strikeIdx++)
			{
				StrikeDescriptor strikeDescriptor = strikes[strikeIdx];
				CardInstance? currentAttacker = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
				if (!currentAttacker.HasValue || currentAttacker.Value.ReadyState == ReadyState.Stunned || currentAttacker.Value.CurrentHitPoints <= 0 || currentAttacker.Value.Wounds >= GetEffectiveMaxHitPoints(state, currentAttacker.Value, engine._cardDb))
				{
					yield break;
				}
				attackerCard = currentAttacker;
				int currentDefId;
				if (strikeIdx == 0)
				{
					currentDefId = declareAttack.DefenderInstanceId;
					CardInstance? initialDefender = CardLocator.FindCard(state, currentDefId);
					if (!initialDefender.HasValue || initialDefender.Value.CurrentHitPoints <= 0 || initialDefender.Value.Wounds >= GetEffectiveMaxHitPoints(state, initialDefender.Value, engine._cardDb))
					{
						yield break;
					}
				}
				else
				{
					List<CardInstance> legalDefendersForStrike = GetLegalDefendersForStrike(state, attackerCard.Value, strikeDescriptor, engine._cardDb, strikeWounds);
					if (legalDefendersForStrike.Count == 0)
					{
						yield break;
					}
					if (engine.SelectStrikeTarget != null)
					{
						StrikeTargetPrompt arg = new StrikeTargetPrompt(state, attackerCard.Value, strikeIdx, strikes.Count, strikeDescriptor, legalDefendersForStrike, previousDefId);
						int? chosen = engine.SelectStrikeTarget(arg);
						if (!chosen.HasValue || !legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == chosen.Value))
						{
							yield break;
						}
						currentDefId = chosen.Value;
					}
					else if (declareAttack.DefenderInstanceIds != null && strikeIdx < declareAttack.DefenderInstanceIds.Length)
					{
						int targetId = declareAttack.DefenderInstanceIds[strikeIdx];
						if (!legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == targetId))
						{
							continue;
						}
						currentDefId = targetId;
					}
					else
					{
						if (!legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == previousDefId))
						{
							continue;
						}
						currentDefId = previousDefId;
					}
				}
					previousDefId = currentDefId;
					CardInstance? currentDefCard = CardLocator.FindCard(state, currentDefId);
					if (currentDefCard.HasValue)
					{
						EffectContext context = new EffectContext
						{
							SourceInstanceId = declareAttack.AttackerInstanceId,
							TargetInstanceId = currentDefId,
							State = state,
							CardDb = engine._cardDb,
							RollDice = diceRoller
						};
						CombatContext combatCtx = new CombatContext
						{
							IsAttacking = true,
							OpponentCard = currentDefCard,
							IsMelee = !strikeDescriptor.IsRanged,
							IsRanged = strikeDescriptor.IsRanged
						};
						CombatContext combatCtx2 = new CombatContext
						{
							IsDefending = true,
							OpponentCard = attackerCard,
							IsMelee = !strikeDescriptor.IsRanged,
							IsRanged = strikeDescriptor.IsRanged
						};
						PassiveBonuses continuousPassiveBonuses2 = GetContinuousPassiveBonuses(state, attackerCard.Value, combatCtx, engine._cardDb);
						PassiveBonuses continuousPassiveBonuses3 = GetContinuousPassiveBonuses(state, currentDefCard.Value, combatCtx2, engine._cardDb);
						int strikeBonus = strikeDescriptor.Modifier + attackerItemBonus + continuousPassiveBonuses2.Atk;
						int strikeAdditionalWounds = declareAttack.AdditionalWounds + continuousPassiveBonuses2.AdditionalWounds + continuousPassiveBonuses3.AdditionalWounds;
						if (!strikeDescriptor.IsRanged && HasTurnBuff(state, declareAttack.AttackerInstanceId, "CLEAVE_HIGHER_LEVEL_WOUND"))
						{
							int effectiveLevel = GetEffectiveLevel(state, attackerCard.Value, engine._cardDb);
							if (GetEffectiveLevel(state, currentDefCard.Value, engine._cardDb) > effectiveLevel)
							{
								strikeAdditionalWounds++;
							}
						}
						AttackResolutionDetails? resolvedDetails = null;
						ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
						{
							StrikeBonus = strikeBonus,
							Damage = 1,
							AdditionalWounds = strikeAdditionalWounds,
							DamageType = (strikeDescriptor.IsRanged ? "ranged" : "melee"),
							EnableCriticals = true,
							TrueStrike = continuousPassiveBonuses2.TrueStrike,
							// Read-only notification: records the outcome and informs listeners, never mutates state.
							InstanceOnAttackResolved = (AttackResolutionDetails details) =>
							{
								resolvedDetails = details;
								if (!state.IsSimulation)
								{
									engine.OnAttackResolved?.Invoke(details);
								}
							}
						};
						foreach (GameEvent item6 in resolveAttackEffect.Evaluate(context))
						{
							yield return item6;
						}
						if (resolvedDetails != null)
						{
							foreach (GameEvent outcomeEvent in GetStrikeOutcomeEvents(engine, state, declareAttack, def, resolvedDetails, currentDefId, currentDefCard.Value, strikeDescriptor, strikeWounds))
							{
								yield return outcomeEvent;
							}
						}
					}
			}
			if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "STIRGES_BONUS_STRIKE"))
				{
					CombatContext combatCtx3 = new CombatContext
					{
						IsAttacking = true,
						OpponentCard = defenderCard,
						IsMelee = true,
						IsRanged = false
					};
					PassiveBonuses continuousPassiveBonuses4 = GetContinuousPassiveBonuses(state, attackerCard.Value, combatCtx3, engine._cardDb);
					PassiveBonuses continuousPassiveBonuses5 = GetContinuousPassiveBonuses(state, defenderCard.Value, new CombatContext
					{
						IsDefending = true,
						OpponentCard = attackerCard,
						IsMelee = true
					}, engine._cardDb);
					int strikeBonus2 = -2 + attackerItemBonus + continuousPassiveBonuses4.Atk;
					int additionalWounds = declareAttack.AdditionalWounds + continuousPassiveBonuses4.AdditionalWounds + continuousPassiveBonuses5.AdditionalWounds;
					ResolveAttackEffect resolveAttackEffect2 = new ResolveAttackEffect
					{
						StrikeBonus = strikeBonus2,
						Damage = 1,
						AdditionalWounds = additionalWounds,
						DamageType = "melee",
						EnableCriticals = true,
						TrueStrike = continuousPassiveBonuses4.TrueStrike,
						InstanceOnAttackResolved = (AttackResolutionDetails details) =>
						{
							if (!state.IsSimulation)
							{
								engine.OnAttackResolved?.Invoke(details);
							}
						}
					};
					EffectContext context2 = new EffectContext
					{
						SourceInstanceId = declareAttack.AttackerInstanceId,
						TargetInstanceId = declareAttack.DefenderInstanceId,
						State = state,
						CardDb = engine._cardDb,
						RollDice = diceRoller
					};
					foreach (GameEvent item7 in resolveAttackEffect2.Evaluate(context2))
					{
						yield return item7;
					}
				}
				if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "CIANE_SELF_WOUND"))
				{
					yield return new DealDamageEvent
					{
						TargetInstanceId = declareAttack.AttackerInstanceId,
						Amount = 1,
						DamageType = "physical"
					};
				}
		}

		public static List<CardInstance> GetLegalDefendersForStrike(GameState state, CardInstance attacker, StrikeDescriptor strike, CardDatabase cardDb, Dictionary<int, int>? pendingWounds = null)
		{
			List<CardInstance> list = new List<CardInstance>();
			int attackerRank = attacker.Rank ?? 1;
			int maxMeleeReach = (CharacterHasReach(attacker, state, cardDb) ? 3 : 2);
			foreach (PlayerState player in state.Players)
			{
				if (player.PlayerIndex == attacker.OwnerPlayerIndex)
				{
					continue;
				}
				foreach (var (defenderRank, rankCharacters) in player.Ranks)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(attackerRank, defenderRank);
					if (strike.IsRanged)
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
					foreach (CardInstance item in rankCharacters)
					{
						if (IsTargetProtectedByStealth(state, attacker, item, strike.IsRanged, cardDb))
						{
							continue;
						}
						int pendingWoundCount = pendingWounds?.GetValueOrDefault(item.InstanceId, 0) ?? 0;
						int effectiveHp = GetEffectiveCurrentHitPoints(state, item, cardDb) - pendingWoundCount;
						int effectiveMaxHitPoints = GetEffectiveMaxHitPoints(state, item, cardDb);
						int effectiveWounds = item.Wounds + pendingWoundCount;
						if (effectiveHp > 0 && effectiveWounds < effectiveMaxHitPoints)
						{
							list.Add(item with
							{
								Wounds = effectiveWounds,
								CurrentHitPoints = effectiveHp
							});
						}
					}
				}
			}
			return list;
		}

		/// <summary>
		/// Converts the outcome of a resolved strike into follow-up GameEvents (on-hit / on-miss effects).
		/// State is not mutated here other than turn-buff bookkeeping; board changes are yielded as events.
		/// </summary>
		private static IEnumerable<GameEvent> GetStrikeOutcomeEvents(GameEngine engine, GameState state, DeclareAttackEvent declareAttack, CardDefinition? def, AttackResolutionDetails details, int currentDefId, CardInstance currentDefCard, StrikeDescriptor strikeDescriptor, Dictionary<int, int> strikeWounds)
		{
			if (details.IsHit)
			{
				if (details.DamageDealt > 0)
				{
					strikeWounds[currentDefId] = strikeWounds.GetValueOrDefault(currentDefId, 0) + details.DamageDealt;
					AddTurnBuff(state, declareAttack.AttackerInstanceId, "WOUNDED_OPPONENT_THIS_TURN", 1);
				}
				if (def != null)
				{
					CardInstance? attackerCard = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
					CardScriptContext strikeCtx = new CardScriptContext
					{
						State = state,
						CardDb = engine._cardDb,
						Card = attackerCard.GetValueOrDefault(),
						Definition = def,
						PerformerId = declareAttack.AttackerInstanceId,
						PlayerIndex = attackerCard?.OwnerPlayerIndex ?? 0
					};
					CardScriptRegistry.OnStrikeHit(strikeCtx);
				}
				if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "CUNNING_SHOT_PULL"))
				{
					CardInstance? targetCard = CardLocator.FindCard(state, currentDefId);
					if (targetCard.HasValue && targetCard.Value.Rank.HasValue && targetCard.Value.Rank.Value > 1)
					{
						yield return new CharacterMovedEvent
						{
							PlayerIndex = targetCard.Value.OwnerPlayerIndex,
							CardInstanceId = currentDefId,
							SourceRank = targetCard.Value.Rank.Value,
							DestinationRank = targetCard.Value.Rank.Value - 1,
							IsForcedMove = true
						};
					}
				}
				CardInstance? defenderStatusCheck = CardLocator.FindCard(state, currentDefId);
				if (defenderStatusCheck.HasValue && defenderStatusCheck.Value.CurrentHitPoints <= 0 && def != null)
				{
					CardInstance? attackerCard = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
					CardScriptContext killCtx = new CardScriptContext
					{
						State = state,
						CardDb = engine._cardDb,
						Card = attackerCard.GetValueOrDefault(),
						Definition = def,
						PerformerId = declareAttack.AttackerInstanceId,
						PlayerIndex = attackerCard?.OwnerPlayerIndex ?? 0
					};
					foreach (GameEvent killEvt in CardScriptRegistry.OnDefenderKilled(killCtx))
					{
						yield return killEvt;
					}
				}
				if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "CRUSH_DESTROY_ITEM"))
				{
					CardInstance? defenderWithItem = CardLocator.FindCard(state, currentDefId);
					if (defenderWithItem.HasValue && defenderWithItem.Value.AttachedItemIds.Count > 0)
					{
						yield return new DestroyItemEvent
						{
							ItemInstanceId = defenderWithItem.Value.AttachedItemIds[0],
							HolderInstanceId = currentDefId
						};
					}
				}
			}
			else
			{
				bool hasPounceAlly = false;
				PlayerState defenderOwnerState = state.Players[currentDefCard.OwnerPlayerIndex];
				if (currentDefCard.Rank.HasValue && defenderOwnerState.Ranks.TryGetValue(currentDefCard.Rank.Value, out List<CardInstance> defenderRankCards))
				{
					foreach (CardInstance rankCard in defenderRankCards)
					{
						if (HasTurnBuff(state, rankCard.InstanceId, "POUNCE_MISS_WOUND"))
						{
							hasPounceAlly = true;
							break;
						}
					}
				}
				if (hasPounceAlly)
				{
					yield return new DealDamageEvent
					{
						TargetInstanceId = declareAttack.AttackerInstanceId,
						Amount = 1,
						DamageType = "physical",
						Unpreventable = true
					};
				}
				if (!strikeDescriptor.IsRanged && CharacterHasRiposte(currentDefCard, state, engine._cardDb))
				{
					CardInstance? riposteDef = CardLocator.FindCard(state, currentDefId);
					if (riposteDef.HasValue && riposteDef.Value.ReadyState != ReadyState.Stunned && riposteDef.Value.CurrentHitPoints > 0 && riposteDef.Value.Wounds < GetEffectiveMaxHitPoints(state, riposteDef.Value, engine._cardDb))
					{
						int riposteBonus = GetRiposteModifier(state, riposteDef.Value, engine._cardDb);
						ResolveAttackEffect riposteStrike = new ResolveAttackEffect
						{
							StrikeBonus = riposteBonus,
							Damage = 1,
							DamageType = "melee",
							EnableCriticals = true,
							InstanceOnAttackResolved = (AttackResolutionDetails rDetails) =>
							{
								if (!state.IsSimulation)
								{
									engine.OnAttackResolved?.Invoke(rDetails);
								}
							}
						};
						EffectContext riposteCtx = new EffectContext
						{
							SourceInstanceId = currentDefId,
							TargetInstanceId = declareAttack.AttackerInstanceId,
							State = state,
							CardDb = engine._cardDb,
							RollDice = engine._diceRoller
						};
						foreach (GameEvent rEvt in riposteStrike.Evaluate(riposteCtx))
						{
							yield return rEvt;
						}
					}
				}
			}
			bool shouldSpurnPush = HasTurnBuff(state, currentDefId, "SHIELD_OF_SPURNING_PUSH");
			if (!shouldSpurnPush)
			{
				foreach (int attachedItemId in currentDefCard.AttachedItemIds)
				{
					CardInstance? attachedItemCard = CardLocator.FindCard(state, attachedItemId);
					if (attachedItemCard.HasValue)
					{
						CardDefinition? itemDef = engine._cardDb.GetDefinition(attachedItemCard.Value.DefinitionId);
						if (itemDef != null && itemDef.RulesText.Contains("Push the attacker one rank backward", StringComparison.OrdinalIgnoreCase))
						{
							shouldSpurnPush = true;
							break;
						}
					}
				}
			}
			if (shouldSpurnPush)
			{
				CardInstance? atkTarget = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
				if (atkTarget.HasValue && atkTarget.Value.Rank.HasValue && atkTarget.Value.Rank.Value < 5)
				{
					yield return new CharacterMovedEvent
					{
						PlayerIndex = atkTarget.Value.OwnerPlayerIndex,
						CardInstanceId = atkTarget.Value.InstanceId,
						SourceRank = atkTarget.Value.Rank.Value,
						DestinationRank = atkTarget.Value.Rank.Value + 1,
						IsForcedMove = true
					};
				}
			}
		}
	}

	private sealed class MagicDamageResistanceHandler(GameEngine engine) : IPostResolutionHandler
	{
		public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
		{
			if (resolvedEvent is DealDamageEvent dmg && string.Equals(dmg.DamageType, "magic", StringComparison.OrdinalIgnoreCase) && dmg.Amount > 0)
			{
				CardInstance? target = FindBoardCard(state, dmg.TargetInstanceId) ?? CardLocator.FindCard(state, dmg.TargetInstanceId);
				if (target.HasValue && CharacterHasMagicResistance(target.Value, state, engine._cardDb))
				{
					int mrRating = GetMagicResistanceRating(state, target.Value, engine._cardDb);
					int effectiveSkill = GetEffectiveSkill(state, target.Value, engine._cardDb);
					int roll = engine._diceRoller(1, 20);
					bool passedCheck = (roll == 20) || (roll != 1 && roll + effectiveSkill + mrRating >= 15);
					int mitigated = passedCheck ? dmg.Amount : Math.Min(dmg.Amount, Math.Max(1, mrRating));
					if (mitigated > 0)
					{
						int maxHp = GetEffectiveMaxHitPoints(state, target.Value, engine._cardDb);
						CardLocator.FindAndMutate(state, dmg.TargetInstanceId, delegate(ref CardInstance c)
						{
							c.Wounds = Math.Max(0, c.Wounds - mitigated);
							c.CurrentHitPoints = Math.Min(maxHp, c.CurrentHitPoints + mitigated);
						});
					}
				}
			}
			yield break;
		}
	}

	private sealed class AttachedItemDefeatHandler : IPostResolutionHandler
	{
		public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
		{
			if (!(resolvedEvent is CharacterDefeatedEvent characterDefeatedEvent))
			{
				yield break;
			}
			PlayerState playerState = state.Players[characterDefeatedEvent.OwnerPlayerIndex];
			List<CardInstance> list = (characterDefeatedEvent.BanishInsteadOfDiscard ? playerState.BanishPile : playerState.DiscardPile);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].InstanceId != characterDefeatedEvent.DefeatedInstanceId)
				{
					continue;
				}
				CardInstance cardInstance = list[i];
				List<int> attachedItemIds = cardInstance.AttachedItemIds;
				if (attachedItemIds.Count <= 0)
				{
					break;
				}
				foreach (int item in attachedItemIds)
				{
					CardInstance? attachedItem = CardLocator.FindCard(state, item);
					int itemOwnerIdx = attachedItem?.OwnerPlayerIndex ?? characterDefeatedEvent.OwnerPlayerIndex;
					PlayerState itemOwnerState = (itemOwnerIdx >= 0 && itemOwnerIdx < state.Players.Count)
						? state.Players[itemOwnerIdx]
						: playerState;
					foreach (PlayerState p in state.Players)
					{
						p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == item);
					}
					itemOwnerState.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == item);
					if (attachedItem.HasValue)
					{
						itemOwnerState.DiscardPile.Add(attachedItem.Value with
						{
							Zone = CardZone.Discard,
							AttachedToInstanceId = null
						});
					}
				}
				list[i] = cardInstance with
				{
					AttachedItemIds = new List<int>()
				};
				break;
			}
		}
	}

	public static bool CharacterHasReach(CardInstance character, GameState state, CardDatabase cardDb)
	{
		if (HasTurnBuff(state, character.InstanceId, "REACH"))
		{
			return true;
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		if (definition != null && definition.HasReach)
		{
			return true;
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition definition2 = cardDb.GetDefinition(cardInstance.Value.DefinitionId);
				if (definition2 != null && definition2.HasReach)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool CharacterHasStealth(CardInstance character, GameState state, CardDatabase cardDb)
	{
		if (HasTurnBuff(state, character.InstanceId, "STEALTH") || character.SkillRatings.ContainsKey("Stealth"))
		{
			return true;
		}
		CardDefinition? definition = cardDb.GetDefinition(character.DefinitionId);
		if (RulesParser.HasStealth(definition))
		{
			return true;
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue && RulesParser.HasStealth(cardDb.GetDefinition(cardInstance.Value.DefinitionId)))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsTargetProtectedByStealth(GameState state, CardInstance attacker, CardInstance defender, bool isRanged, CardDatabase cardDb)
	{
		if (defender.OwnerPlayerIndex == attacker.OwnerPlayerIndex)
		{
			return false;
		}
		if (defender.ReadyState == ReadyState.Stunned || !CharacterHasStealth(defender, state, cardDb))
		{
			return false;
		}
		CardDefinition? atkDef = cardDb.GetDefinition(attacker.DefinitionId);
		bool attackerHasTrueStrike = HasTurnBuff(state, attacker.InstanceId, "TRUE_STRIKE") ||
			GetContinuousPassiveBonuses(state, attacker, new CombatContext { IsAttacking = true, OpponentCard = defender }, cardDb).TrueStrike ||
			(atkDef != null && (atkDef.Traits.Any((string t) => t.Equals("True Strike", StringComparison.OrdinalIgnoreCase)) || atkDef.RulesText.Contains("True Strike", StringComparison.OrdinalIgnoreCase)));
		if (attackerHasTrueStrike)
		{
			return false;
		}
		if (isRanged)
		{
			return true;
		}
		int defRank = defender.Rank ?? 1;
		if (state.Players[defender.OwnerPlayerIndex].Ranks.TryGetValue(defRank, out List<CardInstance>? rankCards))
		{
			bool hasNonStealthInRank = rankCards.Any(c => c.InstanceId != defender.InstanceId && c.CurrentHitPoints > 0 && (c.ReadyState == ReadyState.Stunned || !CharacterHasStealth(c, state, cardDb)));
			if (hasNonStealthInRank)
			{
				return true;
			}
		}
		return false;
	}

	public static bool CharacterHasRiposte(CardInstance character, GameState state, CardDatabase cardDb)
	{
		if (HasTurnBuff(state, character.InstanceId, "RIPOSTE") || character.SkillRatings.ContainsKey("Riposte"))
		{
			return true;
		}
		CardDefinition? definition = cardDb.GetDefinition(character.DefinitionId);
		if (RulesParser.HasRiposte(definition))
		{
			return true;
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue && RulesParser.HasRiposte(cardDb.GetDefinition(cardInstance.Value.DefinitionId)))
			{
				return true;
			}
		}
		return false;
	}

	public static int GetRiposteModifier(GameState state, CardInstance character, CardDatabase cardDb)
	{
		int bonus = GetTurnBuffValue(state, character.InstanceId, "RIPOSTE");
		if (character.SkillRatings.TryGetValue("Riposte", out int rating))
		{
			bonus += rating;
		}
		else
		{
			bonus += RulesParser.GetRiposteRating(cardDb.GetDefinition(character.DefinitionId));
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? item = CardLocator.FindCard(state, attachedItemId);
			if (item.HasValue)
			{
				bonus += RulesParser.GetRiposteRating(cardDb.GetDefinition(item.Value.DefinitionId));
			}
		}
		return GetEffectiveAttack(state, character, new CombatContext { IsAttacking = true, IsMelee = true }, cardDb) + bonus;
	}

	public static bool CharacterHasMagicResistance(CardInstance character, GameState state, CardDatabase cardDb)
	{
		if (HasTurnBuff(state, character.InstanceId, "MAGIC_RESISTANCE") || character.SkillRatings.ContainsKey("Magic Resistance") || character.SkillRatings.ContainsKey("MagicResistance"))
		{
			return true;
		}
		CardDefinition? definition = cardDb.GetDefinition(character.DefinitionId);
		if (RulesParser.HasMagicResistance(definition))
		{
			return true;
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue && RulesParser.HasMagicResistance(cardDb.GetDefinition(cardInstance.Value.DefinitionId)))
			{
				return true;
			}
		}
		return false;
	}

	public static int GetMagicResistanceRating(GameState state, CardInstance character, CardDatabase cardDb)
	{
		int rating = GetTurnBuffValue(state, character.InstanceId, "MAGIC_RESISTANCE");
		if (character.SkillRatings.TryGetValue("Magic Resistance", out int r) || character.SkillRatings.TryGetValue("MagicResistance", out r))
		{
			rating += r;
		}
		else
		{
			rating += RulesParser.GetMagicResistanceRating(cardDb.GetDefinition(character.DefinitionId));
		}
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? item = CardLocator.FindCard(state, attachedItemId);
			if (item.HasValue)
			{
				rating += RulesParser.GetMagicResistanceRating(cardDb.GetDefinition(item.Value.DefinitionId));
			}
		}
		return rating;
	}

}
