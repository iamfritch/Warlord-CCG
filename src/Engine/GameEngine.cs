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

public sealed class GameEngine
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

	private static CardDatabase? _masterCardDb;

	private static CardDatabase? _lastActiveCardDb;

	private readonly GameState _state;

	private readonly EventResolver _resolver;

	private readonly CardDatabase _cardDb;

	private DiceRoller _diceRoller;

	public static readonly DiceRoller DefaultDiceRoller = (int count, int sides) => 15;

	public DiceRoller DiceRoller
	{
		get
		{
			return _diceRoller;
		}
		set
		{
			_diceRoller = value ?? DefaultDiceRoller;
		}
	}

	public bool AutoPassWhenNoReactsAvailable
	{
		get
		{
			return _state.AutoPassWhenNoReactsAvailable;
		}
		set
		{
			_state.AutoPassWhenNoReactsAvailable = value;
		}
	}

	public GameState State => _state;

	public CardDatabase CardDb => _cardDb;

	public EventResolver Resolver => _resolver;

	public (int WinnerIndex, int Roll0, int Roll1)? LastInitiativeRoll { get; private set; }

	public List<(int Roll0, int Roll1)> InitiativeTieHistory { get; } = new List<(int, int)>();

	public Action<AttackResolutionDetails>? OnAttackResolved { get; set; }

	public Func<StrikeTargetPrompt, int?>? SelectStrikeTarget { get; set; }

	public static CardDatabase GetActiveCardDb()
	{
		if (_masterCardDb != null)
		{
			return _masterCardDb;
		}
		string text3 = DataPaths.CardsJsonPath;
		if (File.Exists(text3))
		{
			_masterCardDb = DatabaseLoader.LoadFromFile(text3);
			return _masterCardDb;
		}
		if (_lastActiveCardDb != null)
		{
			return _lastActiveCardDb;
		}
		throw new InvalidOperationException("CardDatabase not initialized and DataParser/cards.json could not be found.");
	}

	public static CardDatabase GetLastActiveCardDb() => _lastActiveCardDb ?? GetActiveCardDb();

	public void SetDiceRoller(DiceRoller roller)
	{
		ArgumentNullException.ThrowIfNull(roller, "roller");
		_diceRoller = roller;
	}

	public void SetRandomSeed(int seed)
	{
		Random rng = new Random(seed);
		_diceRoller = (int count, int sides) =>
		{
			int num = 0;
			for (int i = 0; i < count; i++)
			{
				num += rng.Next(1, sides + 1);
			}
			return num;
		};
	}

	public GameEngine(GameState state, CardDatabase cardDb, DiceRoller? diceRoller = null)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		_state = state;
		_cardDb = cardDb;
		_lastActiveCardDb = cardDb;
		_diceRoller = diceRoller ?? DefaultDiceRoller;
		_resolver = new EventResolver();
		if (_state.PendingEvents.Count > 0)
		{
			_resolver.RestoreStack(_state.PendingEvents);
		}
		_resolver.RegisterPostHandler(new DeathCheckHandler(_cardDb));
		_resolver.RegisterPostHandler(new AttachedItemDefeatHandler());
		_resolver.RegisterPostHandler(new WarlordEliminationHandler());
		_resolver.RegisterPostHandler(new AbilityEffectHandler(_cardDb, (int min, int max) => _diceRoller(1, max), () => OnAttackResolved));
		_resolver.RegisterPostHandler(new AttackResolutionHandler(this));
	}

	public static GameEngine CreateFromDecks(IReadOnlyList<Deck> playerDecks, CardDatabase cardDb, int minDeckSize = 1, DiceRoller? diceRoller = null)
	{
		ArgumentNullException.ThrowIfNull(playerDecks, "playerDecks");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		if (playerDecks.Count < 2)
		{
			throw new ArgumentException("At least two player decks are required.", "playerDecks");
		}
		List<PlayerState> list = new List<PlayerState>();
		int nextInstanceId = 1;
		for (int i = 0; i < playerDecks.Count; i++)
		{
			Deck deck = playerDecks[i];
			ActionResult actionResult = deck.Validate(cardDb, minDeckSize);
			if (!actionResult.IsSuccess)
			{
				throw new InvalidOperationException($"Player {i} deck validation failed: {string.Join("; ", actionResult.Errors)}");
			}
			List<CardInstance> list2 = new List<CardInstance>();
			List<CardInstance> list3 = new List<CardInstance>();
			CardDefinition definition = cardDb.GetDefinition(deck.WarlordDefinitionId);
			list2.Add(new CardInstance
			{
				InstanceId = nextInstanceId++,
				DefinitionId = definition.DefinitionId,
				CurrentHitPoints = definition.BaseHitPoints.GetValueOrDefault(),
				CurrentArmorClass = definition.ArmorClass.GetValueOrDefault(),
				CurrentSkill = definition.Skill.GetValueOrDefault(),
				ReadyState = ReadyState.Ready,
				Zone = CardZone.Hand,
				OwnerPlayerIndex = i,
				SkillRatings = new Dictionary<string, int>(definition.SkillRatings, StringComparer.OrdinalIgnoreCase)
			});
			if (deck.StartingCharacterDefinitionIds.Count > 0)
			{
				foreach (string startingCharacterDefinitionId in deck.StartingCharacterDefinitionIds)
				{
					CardDefinition definition2 = cardDb.GetDefinition(startingCharacterDefinitionId);
					list2.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition2.DefinitionId,
						CurrentHitPoints = definition2.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition2.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition2.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Hand,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition2.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
				foreach (string cardDefinitionId in deck.CardDefinitionIds)
				{
					CardDefinition definition3 = cardDb.GetDefinition(cardDefinitionId);
					list3.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition3.DefinitionId,
						CurrentHitPoints = definition3.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition3.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition3.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Deck,
						OwnerPlayerIndex = i
					});
				}
			}
			else if (deck.CardDefinitionIds.Count <= 5)
			{
				foreach (string cardDefinitionId2 in deck.CardDefinitionIds)
				{
					CardDefinition definition4 = cardDb.GetDefinition(cardDefinitionId2);
					list2.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition4.DefinitionId,
						CurrentHitPoints = definition4.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition4.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition4.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Hand,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition4.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
			}
			else
			{
				foreach (string cardDefinitionId3 in deck.CardDefinitionIds)
				{
					CardDefinition definition5 = cardDb.GetDefinition(cardDefinitionId3);
					list3.Add(new CardInstance
					{
						InstanceId = nextInstanceId++,
						DefinitionId = definition5.DefinitionId,
						CurrentHitPoints = definition5.BaseHitPoints.GetValueOrDefault(),
						CurrentArmorClass = definition5.ArmorClass.GetValueOrDefault(),
						CurrentSkill = definition5.Skill.GetValueOrDefault(),
						ReadyState = ReadyState.Ready,
						Zone = CardZone.Deck,
						OwnerPlayerIndex = i,
						SkillRatings = new Dictionary<string, int>(definition5.SkillRatings, StringComparer.OrdinalIgnoreCase)
					});
				}
			}
			list.Add(new PlayerState
			{
				PlayerIndex = i,
				DisplayName = $"Player {i + 1} ({deck.DeckName})",
				Hand = list2,
				Deck = list3,
				Ranks = new SortedDictionary<int, List<CardInstance>>()
			});
		}
		return new GameEngine(new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Setup,
			NextInstanceId = nextInstanceId,
			Players = list
		}, cardDb, diceRoller);
	}

	public void ShuffleDeck(int playerIndex, Random? rng = null)
	{
		if (playerIndex >= 0 && playerIndex < _state.Players.Count)
		{
			List<CardInstance> deck = _state.Players[playerIndex].Deck;
			Random random = rng ?? new Random();
			for (int num = deck.Count - 1; num > 0; num--)
			{
				int num2 = random.Next(num + 1);
				List<CardInstance> list = deck;
				int index = num;
				List<CardInstance> list2 = deck;
				int index2 = num2;
				CardInstance value = deck[num2];
				CardInstance value2 = deck[num];
				list[index] = value;
				list2[index2] = value2;
			}
		}
	}

	public void ShuffleAllDecks(Random? rng = null)
	{
		Random rng2 = rng ?? new Random();
		for (int i = 0; i < _state.Players.Count; i++)
		{
			ShuffleDeck(i, rng2);
		}
	}

	public ActionResult CanExecute(PlayerAction action, GameState state)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		ArgumentNullException.ThrowIfNull(state, "state");
		if (state.IsGameOver)
		{
			return ActionResult.Failure("Game is over. No further actions can be executed.");
		}
		if (!(action is DeployCharacterAction deploy))
		{
			if (!(action is MarchAction march))
			{
				if (!(action is DeclareAttackAction attack))
				{
					if (!(action is PassPriorityAction))
					{
						if (!(action is PassAction))
						{
							if (!(action is UseAbilityAction useAbility))
							{
								if (!(action is PlayCardAction playCard))
								{
									if (!(action is AttackAction attack2))
									{
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
									return ValidateAttackAction(attack2, state);
								}
								return ValidatePlayCard(playCard, state);
							}
							return ValidateUseAbility(useAbility, state);
						}
						return ValidatePassPriority(action, state);
					}
					return ValidatePassPriority(action, state);
				}
				return ValidateAttack(attack, state);
			}
			return ValidateMarch(march, state);
		}
		return ValidateDeploy(deploy, state);
	}

	public ActionResult CanExecute(PlayerAction action)
	{
		return CanExecute(action, _state);
	}

	public ActionResult CanExecute(IPlayerAction action, GameState state)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		if (action is PlayerAction action2)
		{
			return CanExecute(action2, state);
		}
		return ActionResult.Failure("Unsupported action type: " + action.GetType().Name);
	}

	public ActionResult CanExecute(IPlayerAction action)
	{
		return CanExecute(action, _state);
	}

	public void DiscardCardFromHand(int playerIndex, int cardInstanceId)
	{
		PlayerState player = _state.Players[playerIndex];
		int idx = player.Hand.FindIndex((CardInstance c) => c.InstanceId == cardInstanceId);
		if (idx >= 0)
		{
			CardInstance card = player.Hand[idx];
			player.Hand.RemoveAt(idx);
			player.DiscardPile.Add(card with { Zone = CardZone.Discard });
		}
	}

	public ActionResult Execute(PlayerAction action)
	{
		ActionResult actionResult = CanExecute(action, _state);
		if (!actionResult.IsSuccess)
		{
			return actionResult;
		}
		bool isReactWindowOpen = _state.Priority.IsReactWindowOpen;
		bool flag = ((action is PassAction || action is PassPriorityAction) ? true : false);
		bool flag2 = flag;
		PushEventsForAction(action);
		if (isReactWindowOpen)
		{
			if (flag2)
			{
				int num = Math.Max(2, _state.Players.Count);
				if (_state.Priority.ConsecutivePasses >= num)
				{
					ResolveReactWindowStack();
				}
				SyncPendingEvents();
				return ActionResult.Success();
			}
			_state.Priority.ConsecutivePasses = 0;
			_state.Priority.HasPassed = false;
			if (_state.Players.Count > 0)
			{
				_state.Priority.ActivePlayerIndex = (action.PlayerIndex + 1) % _state.Players.Count;
			}
			SyncPendingEvents();
			return ActionResult.Success();
		}
		DrainResolverStack(action.PlayerIndex);
		SyncPendingEvents();
		if (!_state.Priority.IsReactWindowOpen)
		{
			if (!flag2 && !(action is DeployStartingArmyAction))
			{
				if (_state.CurrentPhase == TurnPhase.Order)
				{
					_state.TurnState.RecordAction();
				}
			}
			else if (flag2 && _state.CurrentPhase == TurnPhase.Main && _state.Priority.ConsecutivePasses >= 2)
			{
				PerformTurnEndSurvivalChecks();
				_state.CurrentPhase = TurnPhase.Ready;
				_state.TurnNumber++;
				_state.Priority.ConsecutivePasses = 0;
				_state.Priority.HasPassed = false;
				UnspendAndUnstunCards();
			}
		}
		return ActionResult.Success();
	}

	public ActionResult Execute(IPlayerAction action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		if (action is PlayerAction action2)
		{
			return Execute(action2);
		}
		return ActionResult.Failure("Unsupported action type: " + action.GetType().Name);
	}

	public ActionResult ExecuteAction(PlayerAction action)
	{
		return Execute(action);
	}

	public ActionResult ExecuteAction(IPlayerAction action)
	{
		return Execute(action);
	}

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

	private static List<int> GetFriendlyBoardCharacterIds(PlayerState player)
	{
		List<int> list = new List<int>();
		foreach (List<CardInstance> value in player.Ranks.Values)
		{
			foreach (CardInstance item in value)
			{
				list.Add(item.InstanceId);
			}
		}
		return list;
	}

	private static CardInstance? FindBoardCard(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item in value)
				{
					if (item.InstanceId == instanceId)
					{
						return item;
					}
				}
			}
		}
		return null;
	}

	public static CardInstance? FindAttachedItem(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (CardInstance item in player.AttachedItems)
			{
				if (item.InstanceId == instanceId)
				{
					return item;
				}
			}
		}
		return null;
	}

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
		int num = performerCard.Rank ?? 1;
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
				CardInstance? cardInstance2 = FindBoardCard(state, marchEvent.CardInstanceId);
				if (!cardInstance2.HasValue || cardInstance2.Value.OwnerPlayerIndex == performerCard.OwnerPlayerIndex)
				{
					failureReason = "Moving character is not an opponent.";
					return false;
				}
				if (ability.MaxRankDistance.HasValue)
				{
					int crossDist = BoardGeometry.GetCrossBoardDistance(num, marchEvent.DestinationRank);
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
				CardInstance? cardInstance3 = FindBoardCard(state, marchEvent.CardInstanceId);
				if (!cardInstance3.HasValue || cardInstance3.Value.OwnerPlayerIndex != performerCard.OwnerPlayerIndex)
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
				CardInstance? cardInstance4 = FindBoardCard(state, dealDamageEvent.TargetInstanceId);
				if (!cardInstance4.HasValue || (cardInstance4.Value.Rank ?? 1) != num || cardInstance4.Value.OwnerPlayerIndex != performerCard.OwnerPlayerIndex)
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
				CardInstance? cardInstance5 = FindBoardCard(state, declareAttackEvent2.AttackerInstanceId);
				if (!cardInstance5.HasValue || Math.Abs((cardInstance5.Value.Rank ?? 1) - num) > ability.MaxRankDistance.Value)
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
				if (!state.Players[sourceCard.OwnerPlayerIndex].Ranks.TryGetValue(num, out var rankCards) || rankCards.Count <= 1)
				{
					failureReason = "Character must be in a rank with at least one other character to move to a different position in this rank.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.OtherInSameRank)
			{
				CardInstance? cardInstance9 = FindBoardCard(state, declareAttackEvent4.DefenderInstanceId);
				if (!cardInstance9.HasValue || (cardInstance9.Value.Rank ?? 1) != num || cardInstance9.Value.InstanceId == sourceCard.InstanceId || cardInstance9.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Defender is not another character in this rank.";
					return false;
				}
			}
			if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue)
			{
				CardInstance? cardInstance10 = FindBoardCard(state, declareAttackEvent4.DefenderInstanceId);
				if (!cardInstance10.HasValue || Math.Abs((cardInstance10.Value.Rank ?? 1) - num) > ability.MaxRankDistance.Value)
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
				CardInstance? cardInstance6 = FindBoardCard(state, declareAttackEvent3.DefenderInstanceId);
				if (!cardInstance6.HasValue || (cardInstance6.Value.Rank ?? 1) != num || cardInstance6.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex)
				{
					failureReason = "Target of the attack is not in this rank.";
					return false;
				}
			}
			if (ability.TargetScope != TargetScope.WithinRankDistance || !ability.MaxRankDistance.HasValue)
			{
				break;
			}
			CardInstance? cardInstance7 = FindBoardCard(state, declareAttackEvent3.AttackerInstanceId);
			if (!cardInstance7.HasValue || Math.Abs((cardInstance7.Value.Rank ?? 1) - num) > ability.MaxRankDistance.Value)
			{
				failureReason = $"Attacker is not within {ability.MaxRankDistance.Value} ranks.";
				return false;
			}
			string description = ability.Description;
			if (description != null && description.Contains("ATK is lower than", StringComparison.OrdinalIgnoreCase))
			{
				CardDefinition definition = _cardDb.GetDefinition(cardInstance7.Value.DefinitionId);
				int num3 = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0) + GetContinuousPassiveBonuses(state, cardInstance7.Value, new CombatContext
				{
					IsAttacking = true
				}, _cardDb).Atk;
				int effectiveLevel = GetEffectiveLevel(state, sourceCard, _cardDb);
				if (num3 >= effectiveLevel)
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
				int num2 = sourceCard.AttachedToInstanceId ?? sourceCard.InstanceId;
				if (ability.TargetScope == TargetScope.Self && characterDefeatedEvent.DefeatedInstanceId != num2)
				{
					failureReason = "This character is not being killed.";
					return false;
				}
				if (ability.TargetScope == TargetScope.SameRank)
				{
					CardInstance? cardInstance = FindBoardCard(state, characterDefeatedEvent.DefeatedInstanceId);
					if (!cardInstance.HasValue || (cardInstance.Value.Rank ?? 1) != num)
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
			CardInstance? cardInstance8 = FindBoardCard(state, abilityActivatedEvent.SourceInstanceId);
			CardDefinition cardDefinition = (cardInstance8.HasValue ? _cardDb.GetDefinition(cardInstance8.Value.DefinitionId) : null);
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
			foreach (int num5 in targetInstanceIds)
			{
				CardInstance? cardInstance11 = FindBoardCard(state, num5);
				if (!cardInstance11.HasValue)
				{
					failureReason = $"Target {num5} not found.";
					return false;
				}
				int num6 = cardInstance11.Value.Rank ?? 1;
				if (ability.TargetScope == TargetScope.SameRank && (num6 != num || cardInstance11.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex))
				{
					failureReason = "Target must be in the same rank as this card.";
					return false;
				}
				if (ability.TargetScope == TargetScope.OtherInSameRank && (num6 != num || cardInstance11.Value.InstanceId == sourceCard.InstanceId || cardInstance11.Value.OwnerPlayerIndex != sourceCard.OwnerPlayerIndex))
				{
					failureReason = "Target must be another character in the same rank.";
					return false;
				}
				if (ability.TargetScope == TargetScope.WithinRankDistance && ability.MaxRankDistance.HasValue && Math.Abs(num6 - num) > ability.MaxRankDistance.Value)
				{
					failureReason = $"Target is not within {ability.MaxRankDistance.Value} ranks.";
					return false;
				}
				if (ability.TargetScope == TargetScope.Self && !ability.Effects.Any((IEffect e) => (e is PerformStrikeEffect || e is DealDamageEffect || e is ResolveAttackEffect) ? true : false) && cardInstance11.Value.InstanceId != sourceCard.InstanceId)
				{
					failureReason = "Target must be this card.";
					return false;
				}
			}
		}
		return true;
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
				yield return new int[1] { list[0] };
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

	public int GetInitiativeModifier(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _state.Players.Count)
		{
			return 0;
		}
		PlayerState playerState = _state.Players[playerIndex];
		int num = 0;
		foreach (List<CardInstance> value in playerState.Ranks.Values)
		{
			foreach (CardInstance item in value)
			{
				num += GetTurnBuffValue(_state, item.InstanceId, "INITIATIVE");
				if (item.SkillRatings.TryGetValue("Initiative", out int initVal))
				{
					num += initVal;
				}
				if (item.SkillRatings.TryGetValue("BonusInitiative", out int bonusInit))
				{
					num += bonusInit;
				}
				CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
				if (definition != null)
				{
					if (definition.SkillRatings.TryGetValue("Initiative", out int defInit) && !item.SkillRatings.ContainsKey("Initiative"))
					{
						num += defInit;
					}
					foreach (string trait in definition.Traits)
					{
						Match match = Regex.Match(trait, @"^\+(\d+)\s+Initiative", RegexOptions.IgnoreCase);
						if (match.Success && int.TryParse(match.Groups[1].Value, out int traitVal))
						{
							num += traitVal;
						}
					}
				}
			}
		}
		foreach (CardInstance attached in playerState.AttachedItems)
		{
			num += GetTurnBuffValue(_state, attached.InstanceId, "INITIATIVE");
			if (attached.SkillRatings.TryGetValue("Initiative", out int initVal2))
			{
				num += initVal2;
			}
			if (attached.SkillRatings.TryGetValue("BonusInitiative", out int bonusInit2))
			{
				num += bonusInit2;
			}
		}
		return num;
	}

	public (int WinnerIndex, int Roll0, int Roll1) RollInitiative()
	{
		InitiativeTieHistory.Clear();
		int num = 0;
		int raw0;
		int raw1;
		int winner = 0;
		do
		{
			raw0 = _diceRoller(1, 20);
			raw1 = _diceRoller(1, 20);
			num++;

			int mod0 = GetInitiativeModifier(0);
			int mod1 = GetInitiativeModifier(1);

			bool isNat20_0 = (raw0 == 20);
			bool isNat20_1 = (raw1 == 20);

			if (isNat20_0 && !isNat20_1)
			{
				winner = 0;
				break;
			}
			if (isNat20_1 && !isNat20_0)
			{
				winner = 1;
				break;
			}

			int total0 = raw0 + mod0;
			int total1 = raw1 + mod1;

			if (total0 == total1)
			{
				if (num < 100)
				{
					InitiativeTieHistory.Add((raw0, raw1));
				}
				continue;
			}

			winner = (total0 > total1) ? 0 : 1;
			break;
		}
		while (num < 100);

		_state.InitiativeWinnerIndex = winner;
		_state.ActivePlayerIndex = winner;
		_state.Priority.ActivePlayerIndex = winner;
		_state.Priority.ConsecutivePasses = 0;
		_state.Priority.HasPassed = false;
		LastInitiativeRoll = (winner, raw0, raw1);
		return (WinnerIndex: winner, Roll0: raw0, Roll1: raw1);
	}

	public void AdvancePhase()
	{
		TurnPhase currentPhase = _state.CurrentPhase;
		TurnPhase turnPhase;
		switch (currentPhase)
		{
		case TurnPhase.Setup:
			turnPhase = TurnPhase.Ready;
			break;
		case TurnPhase.Ready:
			turnPhase = TurnPhase.Draw;
			break;
		case TurnPhase.Draw:
			turnPhase = TurnPhase.Order;
			break;
		case TurnPhase.Order:
		case TurnPhase.Combat:
			turnPhase = TurnPhase.EndOfTurn;
			break;
		case TurnPhase.EndOfTurn:
			PerformTurnEndSurvivalChecks();
			_state.ActivePlayerIndex = (_state.ActivePlayerIndex + 1) % _state.Players.Count;
			_state.TurnNumber++;
			turnPhase = TurnPhase.Ready;
			break;
		default:
			throw new InvalidOperationException($"Unknown phase: {currentPhase}");
		}
		if (turnPhase == TurnPhase.Order)
		{
			RollInitiative();
		}
		if (turnPhase == TurnPhase.Ready)
		{
			UnspendAndUnstunCards();
		}
		if (turnPhase == TurnPhase.Draw)
		{
			foreach (PlayerState player in _state.Players)
			{
				int num = 5 - player.Hand.Count;
				for (int i = 0; i < num && i < player.Deck.Count; i++)
				{
					_resolver.Push(new DrawCardEvent
					{
						PlayerIndex = player.PlayerIndex
					});
				}
			}
		}
		_resolver.Push(new PhaseChangeEvent
		{
			FromPhase = currentPhase,
			ToPhase = turnPhase
		});
		DrainResolverStack();
	}

	private void UnspendAndUnstunCards()
	{
		_state.TurnState.UsedLimitedAbilityKeysThisTurn.Clear();
		foreach (PlayerState player in _state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				for (int i = 0; i < value.Count; i++)
				{
					CardInstance cardInstance = value[i];
					if (cardInstance.AbilitiesUsedThisTurn.Count > 0)
					{
						List<int> list = new List<int>(cardInstance.AbilitiesUsedThisTurn);
						list.Clear();
						cardInstance = cardInstance with
						{
							AbilitiesUsedThisTurn = list
						};
					}
					if (cardInstance.ReadyState == ReadyState.Spent)
					{
						value[i] = cardInstance with
						{
							ReadyState = ReadyState.Ready
						};
					}
					else if (cardInstance.ReadyState == ReadyState.Stunned)
					{
						value[i] = cardInstance with
						{
							ReadyState = ReadyState.Spent
						};
					}
					else
					{
						value[i] = cardInstance;
					}
				}
			}
		}
	}

	private void PerformTurnEndSurvivalChecks()
	{
		List<(int, int)> list = new List<(int, int)>();
		foreach (PlayerState player in _state.Players)
		{
			foreach (List<CardInstance> value in player.Ranks.Values)
			{
				foreach (CardInstance item in value)
				{
					CardDefinition definition = _cardDb.GetDefinition(item.DefinitionId);
					if (definition != null && definition.Name.Equals("Ac'Vuk", StringComparison.OrdinalIgnoreCase) && !HasTurnBuff(_state, item.InstanceId, "WOUNDED_OPPONENT_THIS_TURN"))
					{
						list.Add((player.PlayerIndex, item.InstanceId));
					}
				}
			}
		}
		foreach (var (ownerPlayerIndex, defeatedInstanceId) in list)
		{
			_resolver.Push(new CharacterDefeatedEvent
			{
				DefeatedInstanceId = defeatedInstanceId,
				OwnerPlayerIndex = ownerPlayerIndex
			});
		}
		if (list.Count > 0)
		{
			DrainResolverStack();
		}
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
			if (!_state.Players[playerIndex].Ranks.TryGetValue(rank, out List<CardInstance> value))
			{
				return;
			}
			int num = value.FindIndex((CardInstance c) => c.InstanceId == card.InstanceId);
			if (num >= 0)
			{
				if (num > 0)
				{
					AddTurnBuff(_state, value[num - 1].InstanceId, "ATK", 2);
					AddTurnBuff(_state, value[num - 1].InstanceId, "AC", 2);
				}
				if (num + 1 < value.Count)
				{
					AddTurnBuff(_state, value[num + 1].InstanceId, "ATK", 2);
					AddTurnBuff(_state, value[num + 1].InstanceId, "AC", 2);
				}
			}
		}
		else if (definition.Name.Equals("Frameseer", StringComparison.OrdinalIgnoreCase))
		{
			AddTurnBuff(_state, card.InstanceId, "SURVEYED", 1);
		}
		else
		{
			if (!definition.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase) || !_state.Players[playerIndex].Ranks.TryGetValue(1, out List<CardInstance> value2))
			{
				return;
			}
			foreach (CardInstance item in value2)
			{
				CardDefinition? definition2 = _cardDb.GetDefinition(item.DefinitionId);
				int effectiveLevel = GetEffectiveLevel(_state, item, _cardDb);
				if (IsFreeKingdoms(definition2) && effectiveLevel <= 2)
				{
					AddTurnBuff(_state, item.InstanceId, "ATK", 2);
					AddTurnBuff(_state, item.InstanceId, "AC", 2);
				}
			}
		}
	}

	private ActionResult ValidateDeploy(DeployCharacterAction deploy, GameState state)
	{
		List<string> list = new List<string>();
		if (state.Priority.IsReactWindowOpen)
		{
			list.Add("Cannot deploy characters while a React window is open.");
		}
		if (deploy.PlayerIndex != state.ActivePlayerIndex)
		{
			list.Add($"It is not Player {deploy.PlayerIndex}'s turn. Active player is Player {state.ActivePlayerIndex}.");
		}
		if (state.CurrentPhase != TurnPhase.Order)
		{
			list.Add($"Cannot deploy during {state.CurrentPhase} phase. Deployment is only allowed during the Order phase (Main phase).");
		}
		if (deploy.PlayerIndex < 0 || deploy.PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {deploy.PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		CardDefinition definition = _cardDb.GetDefinition(deploy.DefinitionId);
		if ((object)definition == null)
		{
			list.Add("Card definition '" + deploy.DefinitionId + "' not found in the card database.");
		}
		else if (definition.Type != CardType.Character)
		{
			list.Add($"Card '{definition.Name}' is a {definition.Type}, not a Character. " + "Only Characters can be deployed to a rank.");
		}
		PlayerState playerState = state.Players[deploy.PlayerIndex];
		if (deploy.TargetRank < 1)
		{
			list.Add($"Target rank {deploy.TargetRank} is invalid. Ranks are 1-based.");
		}
		else if (deploy.TargetRank > 1)
		{
			if (!playerState.Ranks.ContainsKey(deploy.TargetRank - 1) || playerState.Ranks[deploy.TargetRank - 1].Count == 0)
			{
				list.Add($"Cannot deploy to Rank {deploy.TargetRank}: Rank {deploy.TargetRank - 1} must be occupied first.");
			}
			else
			{
				int count = playerState.Ranks[deploy.TargetRank - 1].Count;
				int num = (playerState.Ranks.TryGetValue(deploy.TargetRank, out List<CardInstance> value) ? value.Count : 0);
				if (num + 1 > count)
				{
					list.Add($"Cannot deploy to Rank {deploy.TargetRank}: Rank {deploy.TargetRank} cannot have more characters ({num + 1}) than Rank {deploy.TargetRank - 1} ({count}).");
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
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

	private ActionResult ValidateAttack(DeclareAttackAction attack, GameState state)
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
		if (state.CurrentPhase != TurnPhase.Order && state.CurrentPhase != TurnPhase.Combat)
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
			if (player.PlayerIndex == attack.PlayerIndex)
			{
				continue;
			}
			foreach (KeyValuePair<int, List<CardInstance>> rank2 in player.Ranks)
			{
				rank2.Deconstruct(out key, out value);
				int value3 = key;
				foreach (CardInstance item2 in value)
				{
					if (item2.InstanceId == attack.DefenderInstanceId)
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
			list.Add($"Defender {attack.DefenderInstanceId} not found on an opponent's board.");
		}
		else if (num3 == attack.PlayerIndex)
		{
			list.Add("Cannot attack your own character.");
		}
		if (cardInstance.HasValue && cardInstance2.HasValue && num.HasValue && num2.HasValue)
		{
			CardDefinition? definition = _cardDb.GetDefinition(cardInstance.Value.DefinitionId);
			bool num4 = (object)definition != null && definition.Attack?.Strikes.Any((StrikeDescriptor s) => s.IsRanged) == true;
			int rankDistance = BoardGeometry.GetCrossBoardDistance(num.Value, num2.Value);
			if (num4)
			{
				if (rankDistance > 2)
				{
					list.Add($"Ranged attacks reach up to 2 ranks away (distance is {rankDistance}). Attacker is at Rank {num.Value}, Defender is at Rank {num2.Value}.");
				}
			}
			else
			{
				int num5 = (CharacterHasReach(cardInstance.Value, state, _cardDb) ? 3 : 2);
				if (num.Value + num2.Value > num5)
				{
					list.Add($"Melee attacks require rank sum <= {num5}. Attacker is at Rank {num.Value}, Defender is at Rank {num2.Value}.");
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	private static ActionResult ValidatePassPriority(PlayerAction action, GameState state)
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
				CardInstance? cardInstance3 = CardLocator.FindCard(state, useAbility.TargetInstanceIds[0]);
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
				CardInstance? cardInstance4 = CardLocator.FindCard(state, useAbility.TargetInstanceIds[0]);
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
				CardInstance? targetCard = CardLocator.FindCard(state, useAbility.TargetInstanceIds[0]);
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
				CardInstance? targetCard = CardLocator.FindCard(state, useAbility.TargetInstanceIds[0]);
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
		switch (definition.Type)
		{
		case CardType.Character:
		case CardType.Token:
		{
			int num2 = ((definition.Level <= 0) ? 1 : definition.Level);
			int? targetRank = playCard.TargetRank;
			if ((!targetRank.HasValue || targetRank.GetValueOrDefault() < 1) ? true : false)
			{
				list.Add("Playing a Character card requires a valid TargetRank (1-based).");
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
					list.Add($"Cannot deploy character to Rank {playCard.TargetRank}: Rank {playCard.TargetRank} would have {num3 + 1} characters, exceeding Rank {playCard.TargetRank - 1} with {value.Count}.");
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

	private void PushEventsForAction(PlayerAction action)
	{
		if (action is DiscardCardAction discardCardAction)
		{
			DiscardCardFromHand(discardCardAction.PlayerIndex, discardCardAction.CardInstanceId);
			return;
		}
		PlayCardAction playCardAction = action as PlayCardAction;
		if ((object)playCardAction == null)
		{
			if (!(action is DeployCharacterAction deployCharacterAction))
			{
				if (!(action is MarchAction marchAction))
				{
					if (!(action is DeclareAttackAction declareAttackAction))
					{
						if (!(action is AttackAction attackAction))
						{
							if (!(action is DeployStartingArmyAction deployStartingArmyAction))
							{
								if (!(action is PassPriorityAction passPriorityAction))
								{
									if (!(action is PassAction passAction))
									{
										UseAbilityAction useAbilityAction = action as UseAbilityAction;
										if ((object)useAbilityAction == null)
										{
											return;
										}
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
									}
									else
									{
										passAction.Execute(_state);
									}
								}
								else
								{
									_state.Priority.ConsecutivePasses++;
									_state.Priority.HasPassed = true;
									if (_state.Priority.IsReactWindowOpen && _state.Players.Count > 0)
									{
										_state.Priority.ActivePlayerIndex = (passPriorityAction.PlayerIndex + 1) % _state.Players.Count;
									}
								}
								return;
							}
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
						}
						else
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
						}
					}
					else
					{
						CardLocator.FindAndMutate(_state, declareAttackAction.AttackerInstanceId, delegate(ref CardInstance reference)
						{
							reference.ReadyState = ReadyState.Spent;
						});
						_resolver.Push(new DeclareAttackEvent
						{
							AttackerInstanceId = declareAttackAction.AttackerInstanceId,
							DefenderInstanceId = declareAttackAction.DefenderInstanceId,
							DefenderInstanceIds = declareAttackAction.TargetInstanceIds
						});
					}
				}
				else
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
				}
			}
			else
			{
				CardDefinition definition4 = _cardDb.GetDefinition(deployCharacterAction.DefinitionId);
				CardInstance cardInstance6 = CreateCardInstance(definition4, deployCharacterAction.PlayerIndex, CardZone.Hand);
				_state.Players[deployCharacterAction.PlayerIndex].Hand.Add(cardInstance6);
				_resolver.Push(new PlayCardEvent
				{
					PlayerIndex = deployCharacterAction.PlayerIndex,
					CardInstanceId = cardInstance6.InstanceId,
					TargetRank = deployCharacterAction.TargetRank
				});
				TriggerCharacterEnterReacts(cardInstance6, deployCharacterAction.TargetRank, deployCharacterAction.PlayerIndex);
			}
			return;
		}
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
					if (!(playerAction is PassAction) && !(playerAction is PassPriorityAction))
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

	public static (int Atk, int Ac, int Hp) GetItemPassiveStats(CardDefinition? itemDef)
	{
		if (itemDef == null)
		{
			return (Atk: 0, Ac: 0, Hp: 0);
		}
		string text = itemDef.Name.Trim();
		if (text.Equals("Gapfinder", StringComparison.OrdinalIgnoreCase) || text.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase) || text.Equals("Revenger", StringComparison.OrdinalIgnoreCase) || text.Equals("Shillelagh", StringComparison.OrdinalIgnoreCase))
		{
			return (Atk: 0, Ac: 0, Hp: 0);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		string rulesText = itemDef.RulesText;
		if (!string.IsNullOrWhiteSpace(rulesText))
		{
			string[] array = rulesText.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (string input in array)
			{
				if (Regex.IsMatch(input, "^\\s*(?:(?:Spend|Destroy|Limited|Spend Limited)\\s+)?(?:Order|React|Death React|Enter React)\\b", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "^\\s*(?:Destroy|Discard|Sacrifice)\\s+(?:this\\s+(?:item|card)|[A-Za-z]+)\\b", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "\\b(?:during|while|other characters|against|equal to)\\b", RegexOptions.IgnoreCase))
				{
					continue;
				}
				foreach (Match item in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+ATK", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item.Groups[1].Value, out var result))
					{
						num += result;
					}
				}
				foreach (Match item2 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+AC", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item2.Groups[1].Value, out var result2))
					{
						num2 += result2;
					}
				}
				foreach (Match item3 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+HP", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item3.Groups[1].Value, out var result3))
					{
						num3 += result3;
					}
				}
			}
		}
		if (itemDef.ArmorClass.HasValue)
		{
			num2 += itemDef.ArmorClass.Value;
		}
		if (itemDef.Attack?.Strikes.Count > 0)
		{
			num += itemDef.Attack.Strikes[0].Modifier;
		}
		if (itemDef.BaseHitPoints.HasValue)
		{
			num3 += itemDef.BaseHitPoints.Value;
		}
		return (Atk: num, Ac: num2, Hp: num3);
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

	public static void AddTurnBuff(GameState state, int instanceId, string buffType, int value = 0)
	{
		state.TurnState.UsedLimitedAbilityKeysThisTurn.Add($"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:{value}");
	}

	public static bool HasTurnBuff(GameState state, int instanceId, string buffType)
	{
		string prefix = $"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:";
		return state.TurnState.UsedLimitedAbilityKeysThisTurn.Any((string k) => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	public static int GetTurnBuffValue(GameState state, int instanceId, string buffType)
	{
		string text = $"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:";
		int num = 0;
		foreach (string item in state.TurnState.UsedLimitedAbilityKeysThisTurn)
		{
			if (item.StartsWith(text, StringComparison.OrdinalIgnoreCase) && int.TryParse(item.Substring(text.Length), out var result))
			{
				num += result;
			}
		}
		return num;
	}

	public static int ResolvePerformerCharacterId(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		return CardLocator.ResolvePerformerCharacterId(state, instanceId, cardDb);
	}

	public static CardInstance? ResolvePerformerCharacter(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		return CardLocator.ResolvePerformerCharacter(state, instanceId, cardDb);
	}

	public static (int PlayerIndex, int Rank, int Index)? FindCharacterPosition(GameState state, int instanceId)
	{
		for (int i = 0; i < state.Players.Count; i++)
		{
			foreach (var (item, list2) in state.Players[i].Ranks)
			{
				for (int j = 0; j < list2.Count; j++)
				{
					if (list2[j].InstanceId == instanceId)
					{
						return (i, item, j);
					}
				}
			}
		}
		return null;
	}

	public static bool IsDeverenian(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Deverenian", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Deverenian", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsMercenary(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Mercenary", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Mercenary", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsNothrog(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Nothrog", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Nothrog", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool ShareFaction(CardDefinition? a, CardDefinition? b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		if (IsDeverenian(a) && IsDeverenian(b))
		{
			return true;
		}
		if (IsNothrog(a) && IsNothrog(b))
		{
			return true;
		}
		if (IsDwarf(a) && IsDwarf(b))
		{
			return true;
		}
		if (IsFreeKingdoms(a) && IsFreeKingdoms(b))
		{
			return true;
		}
		if (IsMercenary(a) && IsMercenary(b))
		{
			return true;
		}
		bool aElf = (a.GetEffectiveFaction()?.Contains("Elf", StringComparison.OrdinalIgnoreCase) ?? false) || a.Traits.Any((string t) => t.Contains("Elf", StringComparison.OrdinalIgnoreCase));
		bool bElf = (b.GetEffectiveFaction()?.Contains("Elf", StringComparison.OrdinalIgnoreCase) ?? false) || b.Traits.Any((string t) => t.Contains("Elf", StringComparison.OrdinalIgnoreCase));
		if (aElf && bElf)
		{
			return true;
		}
		string? factA = a.GetEffectiveFaction() ?? a.Faction;
		string? factB = b.GetEffectiveFaction() ?? b.Faction;
		if (!string.IsNullOrWhiteSpace(factA) && !string.IsNullOrWhiteSpace(factB) && factA.Equals(factB, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	public static bool IsDwarf(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		string text = def.GetEffectiveFaction() ?? def.Faction ?? "";
		if (!text.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) && !text.Contains("Dwarven", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) || t.Contains("Dwarven", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsFreeKingdoms(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Free Kingdoms", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Free Kingdoms", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsSiege(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Traits.Any((string t) => t.Contains("Siege", StringComparison.OrdinalIgnoreCase)))
			{
				if (def.Class.HasValue)
				{
					return def.Class.Value.ToString().Contains("Siege", StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool IsFighter(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Class.HasValue || !def.Class.Value.ToString().Contains("Fighter", StringComparison.OrdinalIgnoreCase))
			{
				return def.Traits.Any((string t) => t.Contains("Fighter", StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}
		return false;
	}

	public static bool IsWeapon(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Traits.Any((string t) => t.Contains("Weapon", StringComparison.OrdinalIgnoreCase)) && !def.RulesText.Contains("Weapon", StringComparison.OrdinalIgnoreCase))
			{
				if (def.Type == CardType.Item)
				{
					return def.Attack != null;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool HasDualWield(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		return def.Name.Equals("Revenger", StringComparison.OrdinalIgnoreCase)
			|| def.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
			|| def.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsCharacterWounded(CardInstance card, CardDefinition? def, GameState state, CardDatabase? cardDb)
	{
		if (card.Wounds > 0)
		{
			return true;
		}
		int num = def?.BaseHitPoints ?? card.CurrentHitPoints;
		if (cardDb != null)
		{
			foreach (int attachedItemId in card.AttachedItemIds)
			{
				CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
				if (cardInstance.HasValue)
				{
					CardDefinition definition = cardDb.GetDefinition(cardInstance.Value.DefinitionId);
					if (definition != null)
					{
						num += GetItemPassiveStats(definition).Hp;
					}
				}
			}
		}
		return card.CurrentHitPoints < num;
	}

	public static int GetWounds(GameState state, CardInstance card, CardDatabase? cardDb = null)
	{
		if (card.Wounds > 0)
		{
			return card.Wounds;
		}
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int effectiveMaxHitPoints = GetEffectiveMaxHitPoints(state, card, cardDb);
		return Math.Max(0, effectiveMaxHitPoints - card.CurrentHitPoints);
	}

	public PassiveBonuses GetContinuousPassiveBonuses(CardInstance character, CombatContext? combatCtx = null)
	{
		return GetContinuousPassiveBonuses(_state, character, combatCtx, _cardDb);
	}

	public static PassiveBonuses GetContinuousPassiveBonuses(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int num = (definition?.Level ?? 0) + (character.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0);
		bool flag = IsCharacterWounded(character, definition, state, cardDb);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		bool trueStrike = false;
		bool hasReach = false;
		List<StrikeDescriptor> list = new List<StrikeDescriptor>();
		(int, int, int)? tuple = FindCharacterPosition(state, character.InstanceId);
		if (tuple.HasValue)
		{
			(int, int, int) value2 = tuple.Value;
			int item = value2.Item1;
			int item2 = value2.Item2;
			int item3 = value2.Item3;
			List<CardInstance> list2 = state.Players[item].Ranks[item2];
			CardInstance? cardInstance = ((item3 > 0) ? new CardInstance?(list2[item3 - 1]) : ((CardInstance?)null));
			CardInstance? cardInstance2 = ((item3 + 1 < list2.Count) ? new CardInstance?(list2[item3 + 1]) : ((CardInstance?)null));
			int i;
			CardInstance?[] array;
			if (definition != null && definition.Name.Equals("Ambitious Squire", StringComparison.OrdinalIgnoreCase))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance3 = array[i];
					if (cardInstance3.HasValue)
					{
						CardDefinition? definition2 = cardDb.GetDefinition(cardInstance3.Value.DefinitionId);
						int num7 = (definition2?.Level ?? 0) + (cardInstance3.Value.SkillRatings.TryGetValue("BonusLevel", out var value3) ? value3 : 0);
						if (IsDeverenian(definition2) && num7 > num)
							{
								num2 += 2;
							num3 += 2;
						}
					}
				}
			}
			if (IsMercenary(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance4 = array[i];
					if (cardInstance4.HasValue)
					{
						CardDefinition definition3 = cardDb.GetDefinition(cardInstance4.Value.DefinitionId);
						if (definition3 != null && definition3.Name.Equals("Balian", StringComparison.OrdinalIgnoreCase))
						{
							num3 += 2;
							break;
						}
					}
				}
			}
			bool flag2 = definition != null && definition.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase);
			bool flag3 = false;
			array = new CardInstance?[2] { cardInstance, cardInstance2 };
			for (i = 0; i < array.Length; i++)
			{
				CardInstance? cardInstance6 = array[i];
				if (cardInstance6.HasValue)
				{
					CardDefinition definition4 = cardDb.GetDefinition(cardInstance6.Value.DefinitionId);
					if (definition4 != null && definition4.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase))
					{
						flag3 = true;
						break;
					}
				}
			}
			if ((flag2 || flag3) && (character.Wounds > 0 || flag))
			{
				num2 += 5;
			}
			if (IsNothrog(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance7 = array[i];
					if (cardInstance7.HasValue)
					{
						CardDefinition definition5 = cardDb.GetDefinition(cardInstance7.Value.DefinitionId);
						if (definition5 != null && definition5.Name.Equals("Joleg", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (IsSiege(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance8 = array[i];
					if (cardInstance8.HasValue)
					{
						CardDefinition definition6 = cardDb.GetDefinition(cardInstance8.Value.DefinitionId);
						if (definition6 != null && definition6.Name.Equals("Makusog Steelhorn", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (num <= 2)
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance9 = array[i];
					if (cardInstance9.HasValue)
					{
						CardDefinition definition7 = cardDb.GetDefinition(cardInstance9.Value.DefinitionId);
						if (definition7 != null && definition7.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (IsDwarf(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance10 = array[i];
					if (cardInstance10.HasValue)
					{
						CardDefinition definition8 = cardDb.GetDefinition(cardInstance10.Value.DefinitionId);
						if (definition8 != null && definition8.Name.Equals("Tunnel Vanguard", StringComparison.OrdinalIgnoreCase))
						{
							num4++;
						}
					}
				}
			}
			for (int j = 0; j < list2.Count; j++)
			{
				if (j == item3)
				{
					continue;
				}
				foreach (int attachedItemId in list2[j].AttachedItemIds)
				{
					CardInstance? cardInstance11 = CardLocator.FindCard(state, attachedItemId);
					if (cardInstance11.HasValue)
					{
						CardDefinition definition9 = cardDb.GetDefinition(cardInstance11.Value.DefinitionId);
						if (definition9 != null && definition9.Name.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							num3 += 2;
						}
					}
				}
			}
			if ((IsDwarf(definition) & flag) && list2.Any((CardInstance c) =>
			{
				CardDefinition definition13 = cardDb.GetDefinition(c.DefinitionId);
				return definition13 != null && definition13.Name.Equals("Dress", StringComparison.OrdinalIgnoreCase);
			}))
			{
				num3 += 3;
			}
			if (IsFreeKingdoms(definition))
			{
				for (int num8 = 0; num8 < list2.Count; num8++)
				{
					if (num8 == item3)
					{
						continue;
					}
					CardInstance cardInstance12 = list2[num8];
					CardDefinition definition10 = cardDb.GetDefinition(cardInstance12.DefinitionId);
					if (definition10 != null && definition10.Name.Equals("Squire Argos", StringComparison.OrdinalIgnoreCase))
					{
						int num9 = definition10.Level + (cardInstance12.SkillRatings.TryGetValue("BonusLevel", out var value4) ? value4 : 0);
						if (num <= num9)
							{
								num2 += 2;
							num3++;
						}
					}
				}
			}
			if (IsDeverenian(definition) && IsFighter(definition))
			{
				int key = item2 + 1;
				if (state.Players[item].Ranks.TryGetValue(key, out List<CardInstance> value5) && value5.Any((CardInstance c) =>
				{
					CardDefinition definition13 = cardDb.GetDefinition(c.DefinitionId);
					return definition13 != null && definition13.Name.Equals("Taumar the Protector", StringComparison.OrdinalIgnoreCase);
				}))
				{
					num3 += 2;
					num5 += 2;
				}
			}
			for (int num10 = 0; num10 < state.Players.Count; num10++)
			{
				if (num10 == item)
				{
					continue;
				}
				foreach (KeyValuePair<int, List<CardInstance>> rank in state.Players[num10].Ranks)
				{
					rank.Deconstruct(out i, out var value6);
					int num11 = i;
					List<CardInstance> list3 = value6;
					if (BoardGeometry.GetCrossBoardDistance(item2, num11) > 1)
					{
						continue;
					}
					foreach (CardInstance item4 in list3)
					{
						CardDefinition definition11 = cardDb.GetDefinition(item4.DefinitionId);
						if (definition11 != null && definition11.Name.Equals("Ghed Bane", StringComparison.OrdinalIgnoreCase))
						{
							int targetLevel = GetEffectiveLevel(state, character, cardDb);
							int ghedLevel = GetEffectiveLevel(state, item4, cardDb);
							if (targetLevel < ghedLevel)
							{
								num2 -= 2;
							num3 -= 2;
							}
					}
					}
				}
			}
		}
		int num13 = 0;
		bool flag5 = false;
		var attachedItemIds = new HashSet<int>(character.AttachedItemIds);
		var charOwner = state.Players.ElementAtOrDefault(character.OwnerPlayerIndex);
		if (charOwner != null)
		{
			foreach (var it in charOwner.AttachedItems)
			{
				if ((it.AttachedToInstanceId ?? it.AttachedToId) == character.InstanceId)
				{
					attachedItemIds.Add(it.InstanceId);
				}
			}
		}
		foreach (int attachedItemId2 in attachedItemIds)
		{
			CardInstance? cardInstance13 = CardLocator.FindCard(state, attachedItemId2);
			if (!cardInstance13.HasValue)
			{
				continue;
			}
			int? actualAttachedTo = cardInstance13.Value.AttachedToInstanceId ?? cardInstance13.Value.AttachedToId;
			if (actualAttachedTo.HasValue && actualAttachedTo.Value != character.InstanceId)
			{
				continue;
			}
			CardDefinition definition12 = cardDb.GetDefinition(cardInstance13.Value.DefinitionId);
			if (!(definition12 != null))
			{
				continue;
			}
			if (IsWeapon(definition12) || HasDualWield(definition12))
			{
				num13++;
			}
			if (HasDualWield(definition12))
			{
				flag5 = true;
			}
			if (definition12.Name.Equals("Shillelagh", StringComparison.OrdinalIgnoreCase))
			{
				int num14 = character.CurrentSkill + num5;
				int num15 = Math.Max(0, (num14 + 1) / 2);
				num2 += num15;
			}
			if (definition12.Name.Equals("Gapfinder", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				num2 += 5;
			}
			if (definition12.Name.Equals("Scale Mail", StringComparison.OrdinalIgnoreCase) && combatCtx != null && combatCtx.IsDefending && combatCtx.IsRanged)
			{
				num3 += 2;
			}
			if (definition12.Name.Equals("Ardian Greathawk", StringComparison.OrdinalIgnoreCase))
			{
				num5 += 3;
				continue;
			}
			if (definition12.Name.Equals("Unicorn of Thun", StringComparison.OrdinalIgnoreCase))
			{
				num5 += 2;
				continue;
			}
			Match match = Regex.Match(definition12.RulesText, "\\+(\\d+)\\s+Skill", RegexOptions.IgnoreCase);
			if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
			{
				num5 += result;
			}
		}
		if ((flag5 || HasDualWield(definition)) && (object)combatCtx != null && combatCtx.IsMelee && num13 >= 2)
		{
			num6++;
		}
		if (definition != null)
		{
			if (definition.Name.Equals("'Mad' Pat Carrik", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				num6++;
			}
			if (definition.Name.Equals("Blitzer", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				num6++;
			}
			if (definition.Name.Equals("TFK Brigand", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsDefending)
			{
				num6++;
			}
			if (definition.Name.Equals("Grimfey Prowler", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				num2 += character.CurrentSkill + num5;
			}
		}
		string text = $"BUFF:{character.InstanceId}:";
		foreach (string item5 in state.TurnState.UsedLimitedAbilityKeysThisTurn)
		{
			if (!item5.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string[] array2 = item5.Substring(text.Length).Split(':');
			if (array2.Length < 2 || !int.TryParse(array2[1], out var result2))
			{
				continue;
			}
			switch (array2[0].ToUpperInvariant())
			{
			case "ATK":
				num2 += result2;
				break;
			case "AC":
				num3 += result2;
				break;
			case "SKILL":
				num5 += result2;
				break;
			case "HP":
				num4 += result2;
				break;
			case "TRUE_STRIKE":
				trueStrike = true;
				break;
			case "REACH":
				hasReach = true;
				break;
			case "EXTRA_STRIKE":
				list.Add(new StrikeDescriptor
				{
					Modifier = result2,
					IsRanged = false
				});
				break;
			case "MELEE_WOUND_BONUS":
				if ((object)combatCtx != null && combatCtx.IsMelee)
				{
					num6 += result2;
				}
				break;
			case "WOUND_BONUS":
				num6 += result2;
				break;
			}
		}
		if (character.SkillRatings.TryGetValue("BonusAtk", out var value8))
		{
			num2 += value8;
		}
		return new PassiveBonuses
		{
			Atk = num2,
			Ac = num3,
			Hp = num4,
			Skill = num5,
			AdditionalWounds = num6,
			TrueStrike = trueStrike,
			HasReach = hasReach,
			AdditionalStrikes = list
		};
	}

	public static int GetEffectiveAttack(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int num = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0);
		int num2 = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(cardInstance.Value.DefinitionId);
				num2 += GetItemPassiveStats(itemDef).Atk;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return num + num2 + continuousPassiveBonuses.Atk;
	}

	public static int GetEffectiveArmorClass(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return character.CurrentArmorClass + continuousPassiveBonuses.Ac;
	}

	public static int GetEffectiveCurrentHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentHitPoints + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveMaxHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int num = (cardDb?.GetDefinition(character.DefinitionId))?.BaseHitPoints ?? character.CurrentHitPoints;
		int num2 = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(cardInstance.Value.DefinitionId);
				num2 += GetItemPassiveStats(itemDef).Hp;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return num + num2 + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveSkill(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentSkill + continuousPassiveBonuses.Skill;
	}

	public static int GetEffectiveLevel(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int value;
		return (cardDb.GetDefinition(character.DefinitionId)?.Level ?? 0) + character.BonusLevel + (character.SkillRatings.TryGetValue("BonusLevel", out value) ? value : 0);
	}

	public static IReadOnlyList<StrikeDescriptor> GetEffectiveStrikes(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		List<StrikeDescriptor> list = new List<StrikeDescriptor>();
		if ((object)definition != null && definition.Attack?.Strikes.Count > 0)
		{
			list.AddRange(definition.Attack.Strikes);
		}
		else
		{
			list.Add(new StrikeDescriptor
			{
				Modifier = 0,
				IsRanged = false
			});
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, new CombatContext
		{
			IsAttacking = true
		}, cardDb);
		if (continuousPassiveBonuses.AdditionalStrikes.Count > 0)
		{
			list.AddRange(continuousPassiveBonuses.AdditionalStrikes);
		}
		return list;
	}
}
