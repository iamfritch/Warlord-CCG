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
				foreach (List<CardInstance> value7 in player.Ranks.Values)
				{
					foreach (CardInstance item2 in value7)
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
				foreach (List<CardInstance> value8 in player2.Ranks.Values)
				{
					foreach (CardInstance item3 in value8)
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
				int num = 8 + defSkill;
				int baseAtkSkill = (engine._cardDb.GetDefinition(attackerCard.Value.DefinitionId)?.Skill).GetValueOrDefault();
				int atkSkill = (attackerCard.Value.CurrentSkill > 0 ? attackerCard.Value.CurrentSkill : baseAtkSkill) + GetTurnBuffValue(state, attackerCard.Value.InstanceId, "SKILL");
				if (engine._diceRoller(1, 20) + atkSkill < num)
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
			bool flag = HasTurnBuff(state, declareAttack.AttackerInstanceId, "ADVANTAGE");
			bool flagDis = HasTurnBuff(state, declareAttack.AttackerInstanceId, "DISADVANTAGE");
			DiceRoller diceRoller = engine._diceRoller;
			if (flag && !flagDis)
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
			else if (flagDis && !flag)
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
			int strikeIdx = 0;
			while (true)
			{
				if (strikeIdx < strikes.Count)
				{
					StrikeDescriptor strikeDescriptor = strikes[strikeIdx];
					CardInstance? cardInstance2 = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
					if (!cardInstance2.HasValue || cardInstance2.Value.ReadyState == ReadyState.Stunned || cardInstance2.Value.CurrentHitPoints <= 0 || cardInstance2.Value.Wounds >= GetEffectiveMaxHitPoints(state, cardInstance2.Value, engine._cardDb))
					{
						break;
					}
					attackerCard = cardInstance2;
					int currentDefId;
					if (strikeIdx == 0)
					{
						currentDefId = declareAttack.DefenderInstanceId;
						CardInstance? cardInstance3 = CardLocator.FindCard(state, currentDefId);
						if (!cardInstance3.HasValue || cardInstance3.Value.CurrentHitPoints <= 0 || cardInstance3.Value.Wounds >= GetEffectiveMaxHitPoints(state, cardInstance3.Value, engine._cardDb))
						{
							break;
						}
					}
					else
					{
						List<CardInstance> legalDefendersForStrike = GetLegalDefendersForStrike(state, attackerCard.Value, strikeDescriptor, engine._cardDb, strikeWounds);
						if (legalDefendersForStrike.Count == 0)
						{
							break;
						}
						if (engine.SelectStrikeTarget != null)
						{
							StrikeTargetPrompt arg = new StrikeTargetPrompt(state, attackerCard.Value, strikeIdx, strikes.Count, strikeDescriptor, legalDefendersForStrike, previousDefId);
							int? chosen = engine.SelectStrikeTarget(arg);
							if (!chosen.HasValue || !legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == chosen.Value))
							{
								break;
							}
							currentDefId = chosen.Value;
						}
						else if (declareAttack.DefenderInstanceIds != null && strikeIdx < declareAttack.DefenderInstanceIds.Length)
						{
							int targetId = declareAttack.DefenderInstanceIds[strikeIdx];
							if (!legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == targetId))
							{
								goto IL_0d35;
							}
							currentDefId = targetId;
						}
						else
						{
							if (!legalDefendersForStrike.Any((CardInstance c) => c.InstanceId == previousDefId))
							{
								goto IL_0d35;
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
						int num2 = declareAttack.AdditionalWounds + continuousPassiveBonuses2.AdditionalWounds + continuousPassiveBonuses3.AdditionalWounds;
						if (!strikeDescriptor.IsRanged && HasTurnBuff(state, declareAttack.AttackerInstanceId, "CLEAVE_HIGHER_LEVEL_WOUND"))
						{
							int effectiveLevel = GetEffectiveLevel(state, attackerCard.Value, engine._cardDb);
							if (GetEffectiveLevel(state, currentDefCard.Value, engine._cardDb) > effectiveLevel)
							{
								num2++;
							}
						}
						ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
						{
							StrikeBonus = strikeBonus,
							Damage = 1,
							AdditionalWounds = num2,
							DamageType = (strikeDescriptor.IsRanged ? "ranged" : "melee"),
							EnableCriticals = true,
							TrueStrike = continuousPassiveBonuses2.TrueStrike,
							InstanceOnAttackResolved = (AttackResolutionDetails details) =>
							{
								if (!state.IsSimulation)
								{
									engine.OnAttackResolved?.Invoke(details);
								}
								if (details.IsHit)
								{
									if (details.DamageDealt > 0)
									{
										strikeWounds[currentDefId] = strikeWounds.GetValueOrDefault(currentDefId, 0) + details.DamageDealt;
										AddTurnBuff(state, declareAttack.AttackerInstanceId, "WOUNDED_OPPONENT_THIS_TURN", 1);
									}
									if (def != null && def.Name.Equals("Stirges", StringComparison.OrdinalIgnoreCase) && !HasTurnBuff(state, declareAttack.AttackerInstanceId, "STIRGES_BONUS_STRIKE"))
									{
										AddTurnBuff(state, declareAttack.AttackerInstanceId, "STIRGES_BONUS_STRIKE", 1);
									}
									if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "CUNNING_SHOT_PULL"))
									{
										CardInstance? targetCard = CardLocator.FindCard(state, currentDefId);
										if (targetCard.HasValue && targetCard.Value.Rank.HasValue && targetCard.Value.Rank.Value > 1)
										{
											int value2 = targetCard.Value.Rank.Value;
											int num3 = value2 - 1;
											PlayerState playerState2 = state.Players[targetCard.Value.OwnerPlayerIndex];
											playerState2.Ranks[value2].RemoveAll((CardInstance c) => c.InstanceId == targetCard.Value.InstanceId);
											if (!playerState2.Ranks.TryGetValue(num3, out List<CardInstance> value3))
											{
												value3 = new List<CardInstance>();
												playerState2.Ranks[num3] = value3;
											}
											value3.Add(targetCard.Value with
											{
												Rank = num3
											});
										}
									}
									CardInstance? cardInstance4 = CardLocator.FindCard(state, currentDefId);
									if (cardInstance4.HasValue && cardInstance4.Value.CurrentHitPoints <= 0)
									{
										if (def != null && def.Name.Equals("Mistress Allandra", StringComparison.OrdinalIgnoreCase))
										{
											CardLocator.FindAndMutate(state, declareAttack.AttackerInstanceId, delegate(ref CardInstance c)
											{
												c.ReadyState = ReadyState.Ready;
											});
										}
										if (def != null && def.Name.Equals("Tunnel Worm", StringComparison.OrdinalIgnoreCase))
										{
											(int, int, int)? tuple = FindCharacterPosition(state, declareAttack.AttackerInstanceId);
											if (tuple.HasValue)
											{
												List<CardInstance> list = state.Players[tuple.Value.Item1].Ranks[tuple.Value.Item2];
												if (list.Count > 1)
												{
													int targetSlot = (tuple.Value.Item3 == 0) ? list.Count - 1 : 0;
													engine._resolver.Push(new CharacterMovedEvent
													{
														PlayerIndex = tuple.Value.Item1,
														CardInstanceId = declareAttack.AttackerInstanceId,
														SourceRank = tuple.Value.Item2,
														DestinationRank = tuple.Value.Item2,
														TargetSlotIndex = targetSlot
													});
												}
											}
										}
									}
									if (HasTurnBuff(state, declareAttack.AttackerInstanceId, "CRUSH_DESTROY_ITEM"))
									{
										CardInstance? cardInstance5 = CardLocator.FindCard(state, currentDefId);
										if (cardInstance5.HasValue && cardInstance5.Value.AttachedItemIds.Count > 0)
										{
											int itemToDestroy = cardInstance5.Value.AttachedItemIds[0];
											CardLocator.FindAndMutate(state, currentDefId, delegate(ref CardInstance c)
											{
												c.AttachedItemIds.Remove(itemToDestroy);
											});
											CardLocator.FindAndMutate(state, itemToDestroy, delegate(ref CardInstance c)
											{
												c.Zone = CardZone.Discard;
												c.AttachedToInstanceId = null;
											});
											PlayerState playerState3 = state.Players[cardInstance5.Value.OwnerPlayerIndex];
											foreach (PlayerState p in state.Players)
											{
												p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == itemToDestroy);
											}
											CardInstance? cardInstance6 = CardLocator.FindCard(state, itemToDestroy);
											if (cardInstance6.HasValue && !playerState3.DiscardPile.Any((CardInstance c) => c.InstanceId == itemToDestroy))
											{
												playerState3.DiscardPile.Add(cardInstance6.Value with
												{
													Zone = CardZone.Discard,
													AttachedToInstanceId = null
												});
											}
										}
									}
								}
								else
								{
									bool flag2 = false;
									PlayerState playerState4 = state.Players[currentDefCard.Value.OwnerPlayerIndex];
									if (currentDefCard.Value.Rank.HasValue && playerState4.Ranks.TryGetValue(currentDefCard.Value.Rank.Value, out List<CardInstance> value4))
									{
										foreach (CardInstance item5 in value4)
										{
											if (HasTurnBuff(state, item5.InstanceId, "POUNCE_MISS_WOUND"))
											{
												flag2 = true;
												break;
											}
										}
									}
									if (flag2)
									{
										CardLocator.FindAndMutate(state, declareAttack.AttackerInstanceId, delegate(ref CardInstance c)
										{
											c.CurrentHitPoints = Math.Max(0, c.CurrentHitPoints - 1);
										});
									}
									if (!strikeDescriptor.IsRanged && currentDefCard.HasValue && CharacterHasRiposte(currentDefCard.Value, state, engine._cardDb))
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
												engine._resolver.Push(rEvt);
											}
										}
									}
								}
								bool flag3 = HasTurnBuff(state, currentDefId, "SHIELD_OF_SPURNING_PUSH");
								if (!flag3 && currentDefCard.HasValue)
								{
									foreach (int attachedItemId2 in currentDefCard.Value.AttachedItemIds)
									{
										CardInstance? cardInstance7 = CardLocator.FindCard(state, attachedItemId2);
										if (cardInstance7.HasValue)
										{
											CardDefinition definition2 = engine._cardDb.GetDefinition(cardInstance7.Value.DefinitionId);
											if (definition2 != null && definition2.RulesText.Contains("Push the attacker one rank backward", StringComparison.OrdinalIgnoreCase))
											{
												flag3 = true;
												break;
											}
										}
									}
								}
								if (flag3)
								{
									CardInstance? atkTarget = CardLocator.FindCard(state, declareAttack.AttackerInstanceId);
									if (atkTarget.HasValue && atkTarget.Value.Rank.HasValue && atkTarget.Value.Rank.Value < 5)
									{
										int value5 = atkTarget.Value.Rank.Value;
										int num4 = value5 + 1;
										PlayerState playerState5 = state.Players[atkTarget.Value.OwnerPlayerIndex];
										playerState5.Ranks[value5].RemoveAll((CardInstance c) => c.InstanceId == atkTarget.Value.InstanceId);
										if (!playerState5.Ranks.TryGetValue(num4, out List<CardInstance> value6))
										{
											value6 = new List<CardInstance>();
											playerState5.Ranks[num4] = value6;
										}
										value6.Add(atkTarget.Value with
										{
											Rank = num4
										});
									}
								}
							}
						};
						foreach (GameEvent item6 in resolveAttackEffect.Evaluate(context))
						{
							yield return item6;
						}
					}
					goto IL_0d35;
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
				break;
				IL_0d35:
				strikeIdx++;
			}
		}

		public static List<CardInstance> GetLegalDefendersForStrike(GameState state, CardInstance attacker, StrikeDescriptor strike, CardDatabase cardDb, Dictionary<int, int>? pendingWounds = null)
		{
			List<CardInstance> list = new List<CardInstance>();
			int num = attacker.Rank ?? 1;
			int num2 = (CharacterHasReach(attacker, state, cardDb) ? 3 : 2);
			foreach (PlayerState player in state.Players)
			{
				if (player.PlayerIndex == attacker.OwnerPlayerIndex)
				{
					continue;
				}
				foreach (var (num4, list3) in player.Ranks)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(num, num4);
					if (strike.IsRanged)
					{
						if (!BoardGeometry.IsRangedValid(num, num4, 2))
						{
							continue;
						}
					}
					else if (crossDist > num2 - 1)
					{
						continue;
					}
					foreach (CardInstance item in list3)
					{
						if (IsTargetProtectedByStealth(state, attacker, item, strike.IsRanged, cardDb))
						{
							continue;
						}
						int num5 = pendingWounds?.GetValueOrDefault(item.InstanceId, 0) ?? 0;
						int num6 = GetEffectiveCurrentHitPoints(state, item, cardDb) - num5;
						int effectiveMaxHitPoints = GetEffectiveMaxHitPoints(state, item, cardDb);
						int num7 = item.Wounds + num5;
						if (num6 > 0 && num7 < effectiveMaxHitPoints)
						{
							list.Add(item with
							{
								Wounds = num7,
								CurrentHitPoints = num6
							});
						}
					}
				}
			}
			return list;
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
					CardInstance? cardInstance2 = CardLocator.FindCard(state, item);
					int itemOwnerIdx = cardInstance2?.OwnerPlayerIndex ?? characterDefeatedEvent.OwnerPlayerIndex;
					PlayerState itemOwnerState = (itemOwnerIdx >= 0 && itemOwnerIdx < state.Players.Count)
						? state.Players[itemOwnerIdx]
						: playerState;
					foreach (PlayerState p in state.Players)
					{
						p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == item);
					}
					itemOwnerState.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == item);
					if (cardInstance2.HasValue)
					{
						itemOwnerState.DiscardPile.Add(cardInstance2.Value with
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
