using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record PerformStrikeEffect(int StrikeBonus = 0, bool IsRanged = false, int AdditionalWounds = 0, bool StunOnHit = false, bool SpendOnHit = false, bool PushOnHit = false, bool PullOnHit = false, bool DestroyItemOnHit = false, bool ZeroDamage = false, bool BonusFromLevel = false, bool TargetSpentOnly = false, bool TargetStunnedOnly = false, bool TargetSpentOrStunnedOnly = false, bool TargetLowerLevelOnly = false, bool BonusWoundAgainstHigherLevel = false, bool HasReach = false, bool TrueStrike = false, bool HasAdvantage = false, bool NoRollBonuses = false, bool CannotReduceWounds = false, bool BanishOnKill = false, int MaxRange = 2, bool HasDisadvantage = false, bool IgnoreRange = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int? targetIdOpt = context.TargetInstanceId;
		int attackerInstanceId = context.GetPerformerCharacterId();
		CardInstance? attacker = context.GetPerformerCharacter();

		if (!targetIdOpt.HasValue)
		{
			if (attacker.HasValue)
			{
				int? aRank = BoardGeometry.FindCardRank(context.State.Players[attacker.Value.OwnerPlayerIndex], attackerInstanceId);
				if (aRank.HasValue)
				{
					int oppIdx = 1 - attacker.Value.OwnerPlayerIndex;
					if (oppIdx >= 0 && oppIdx < context.State.Players.Count)
					{
						foreach (var (r, rCards) in context.State.Players[oppIdx].Ranks)
						{
							int d = BoardGeometry.GetCrossBoardDistance(aRank.Value, r);
							int maxMelee = HasReach ? 2 : 1;
							if (IgnoreRange || (IsRanged ? BoardGeometry.IsRangedValid(aRank.Value, r, MaxRange) : d <= maxMelee))
							{
								var valid = rCards.FirstOrDefault(c => (!TargetSpentOnly || c.ReadyState == ReadyState.Spent) && (!TargetStunnedOnly || c.ReadyState == ReadyState.Stunned) && (!TargetSpentOrStunnedOnly || c.ReadyState != ReadyState.Ready));
								if (valid.InstanceId != 0)
								{
									targetIdOpt = valid.InstanceId;
									break;
								}
							}
						}
					}
					if (!targetIdOpt.HasValue)
					{
						var fPlayer = context.State.Players[attacker.Value.OwnerPlayerIndex];
						foreach (var (r, rCards) in fPlayer.Ranks)
						{
							int d = Math.Max(1, Math.Abs(aRank.Value - r));
							int maxMelee = HasReach ? 2 : 1;
							if (IgnoreRange || d <= maxMelee)
							{
								var valid = rCards.FirstOrDefault(c => c.InstanceId != attackerInstanceId && (!TargetSpentOnly || c.ReadyState == ReadyState.Spent) && (!TargetStunnedOnly || c.ReadyState == ReadyState.Stunned) && (!TargetSpentOrStunnedOnly || c.ReadyState != ReadyState.Ready));
								if (valid.InstanceId != 0)
								{
									targetIdOpt = valid.InstanceId;
									break;
								}
							}
						}
					}
				}
			}
		}
		if (!targetIdOpt.HasValue)
		{
			yield break;
		}
		int targetId = targetIdOpt.Value;
		CardInstance? defender = context.FindCard(targetId);
		if (!defender.HasValue || (TargetSpentOnly && defender.Value.ReadyState != ReadyState.Spent) || (TargetStunnedOnly && defender.Value.ReadyState != ReadyState.Stunned) || (TargetSpentOrStunnedOnly && defender.Value.ReadyState != ReadyState.Spent && defender.Value.ReadyState != ReadyState.Stunned))
		{
			yield break;
		}
		CardInstance? opponentCard = attacker;
		PassiveBonuses earlyPassiveBonuses = (opponentCard.HasValue ? GameEngine.GetContinuousPassiveBonuses(context.State, opponentCard.Value, new CombatContext { IsAttacking = true, OpponentCard = defender, IsMelee = !IsRanged, IsRanged = IsRanged }, context.CardDb) : new PassiveBonuses());
		bool hasTrueStrike = TrueStrike || earlyPassiveBonuses.TrueStrike;
		if (!hasTrueStrike && !IsRanged && GameEngine.HasTurnBuff(context.State, targetId, "DUPE_ACTIVE"))
		{
			int baseDefSkill = (context.CardDb?.GetDefinition(defender.Value.DefinitionId)?.Skill).GetValueOrDefault();
			int defSkill = (defender.Value.CurrentSkill > 0 ? defender.Value.CurrentSkill : baseDefSkill) + GameEngine.GetTurnBuffValue(context.State, defender.Value.InstanceId, "SKILL");
			int dc = 8 + defSkill;
			int baseAtkSkill = ((opponentCard.HasValue && context.CardDb != null) ? (context.CardDb.GetDefinition(opponentCard.Value.DefinitionId)?.Skill).GetValueOrDefault() : 0);
			int atkSkill = (opponentCard.HasValue ? (opponentCard.Value.CurrentSkill > 0 ? opponentCard.Value.CurrentSkill : baseAtkSkill) + GameEngine.GetTurnBuffValue(context.State, opponentCard.Value.InstanceId, "SKILL") : 0);
			if (context.RollDice(1, 20) + atkSkill < dc && opponentCard.HasValue)
			{
				GameEngine.AddTurnBuff(context.State, opponentCard.Value.InstanceId, "NO_MELEE_STRIKES", 1);
			}
			yield break;
		}
		if (opponentCard.HasValue && !IgnoreRange)
		{
			int? atkRank = BoardGeometry.FindCardRank(context.State.Players[opponentCard.Value.OwnerPlayerIndex], opponentCard.Value.InstanceId);
			int? defRank = BoardGeometry.FindCardRank(context.State.Players[defender.Value.OwnerPlayerIndex], defender.Value.InstanceId);
			if (atkRank.HasValue && defRank.HasValue)
			{
				if (IsRanged)
				{
					if (!BoardGeometry.IsRangedValid(atkRank.Value, defRank.Value, MaxRange))
					{
						yield break;
					}
				}
				else
				{
					int dist;
					if (opponentCard.Value.OwnerPlayerIndex == defender.Value.OwnerPlayerIndex)
					{
						dist = Math.Max(1, Math.Abs(atkRank.Value - defRank.Value));
					}
					else
					{
						dist = BoardGeometry.GetCrossBoardDistance(atkRank.Value, defRank.Value);
					}
					int maxMelee = HasReach ? 2 : 1;
					if (dist > maxMelee)
					{
						yield break;
					}
				}
			}
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (opponentCard.HasValue)
		{
			CardDefinition definition = context.CardDb.GetDefinition(opponentCard.Value.DefinitionId);
			num2 = (definition?.Level ?? 0) + opponentCard.Value.BonusLevel + (opponentCard.Value.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0);
			if ((object)definition != null && definition.Attack?.Strikes.Count > 0)
			{
				num = definition.Attack.Strikes[0].Modifier;
			}
			foreach (int attachedItemId in opponentCard.Value.AttachedItemIds)
			{
				CardInstance? cardInstance = context.FindCard(attachedItemId);
				if (cardInstance.HasValue)
				{
					CardDefinition definition2 = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
					if (definition2 != null)
					{
						num3 += GameEngine.GetItemPassiveStats(definition2).Atk;
					}
				}
			}
		}
		CombatContext combatCtx = new CombatContext
		{
			IsAttacking = true,
			OpponentCard = defender,
			IsMelee = !IsRanged,
			IsRanged = IsRanged
		};
		CombatContext combatCtx2 = new CombatContext
		{
			IsDefending = true,
			OpponentCard = opponentCard,
			IsMelee = !IsRanged,
			IsRanged = IsRanged
		};
		PassiveBonuses passiveBonuses = (opponentCard.HasValue ? GameEngine.GetContinuousPassiveBonuses(context.State, opponentCard.Value, combatCtx, context.CardDb) : new PassiveBonuses());
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(context.State, defender.Value, combatCtx2, context.CardDb);
		CardDefinition definition3 = context.CardDb.GetDefinition(defender.Value.DefinitionId);
		int num4 = (definition3?.Level ?? 0) + defender.Value.BonusLevel + (defender.Value.SkillRatings.TryGetValue("BonusLevel", out var value2) ? value2 : 0);
		if (TargetLowerLevelOnly && num4 >= num2)
		{
			yield break;
		}
		int num5 = ((!NoRollBonuses) ? (num + num3 + StrikeBonus + passiveBonuses.Atk) : 0);
		if (BonusFromLevel && !NoRollBonuses)
		{
			num5 += num2;
		}
		int num6 = definition3?.ArmorClass ?? 10;
		int num7 = 0;
		foreach (int attachedItemId2 in defender.Value.AttachedItemIds)
		{
			CardInstance? cardInstance2 = context.FindCard(attachedItemId2);
			if (cardInstance2.HasValue)
			{
				CardDefinition definition4 = context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId);
				if (definition4 != null)
				{
					num7 += GameEngine.GetItemPassiveStats(definition4).Ac;
				}
			}
		}
		int num8 = Math.Max(defender.Value.CurrentArmorClass, num6 + num7) + continuousPassiveBonuses.Ac;
		int num9 = AdditionalWounds + passiveBonuses.AdditionalWounds + continuousPassiveBonuses.AdditionalWounds;
		if (BonusWoundAgainstHigherLevel && defender.HasValue && num4 > num2)
		{
			num9++;
		}
		bool flag = TrueStrike || passiveBonuses.TrueStrike;
		bool num10 = HasAdvantage || (opponentCard.HasValue && GameEngine.HasTurnBuff(context.State, opponentCard.Value.InstanceId, "ADVANTAGE"));
		bool hasDisadvantage = HasDisadvantage || (opponentCard.HasValue && GameEngine.HasTurnBuff(context.State, opponentCard.Value.InstanceId, "DISADVANTAGE"));
		int num11 = context.RollDice(1, 20);
		int num12 = num11;
		if (num10 && !hasDisadvantage)
		{
			int val = context.RollDice(1, 20);
			num12 = Math.Max(num11, val);
		}
		else if (hasDisadvantage && !num10)
		{
			int val2 = context.RollDice(1, 20);
			num12 = Math.Min(num11, val2);
		}
		bool flag2 = num12 == 1;
		bool flag3 = num12 == 20;
		int num13 = num12 + num5;
		bool flag4 = !flag2 && (flag3 || num13 >= num8);
		int damageDealt = ((flag4 && !ZeroDamage) ? (1 + num9) : 0);
		string attackerName = ((!opponentCard.HasValue || context.CardDb == null) ? $"Card #{attackerInstanceId}" : (context.CardDb.GetDefinition(opponentCard.Value.DefinitionId)?.Name ?? $"Card #{attackerInstanceId}"));
		string defenderName = definition3?.Name ?? $"Card #{targetId}";
		AttackResolutionDetails obj = new AttackResolutionDetails
		{
			AttackerInstanceId = attackerInstanceId,
			DefenderInstanceId = targetId,
			AttackerName = attackerName,
			DefenderName = defenderName,
			Roll = num12,
			StrikeBonus = num5,
			DefenderArmorClass = num8,
			IsCritical = flag3,
			IsCriticalFailure = flag2,
			IsHit = flag4,
			DamageDealt = damageDealt,
			DamageType = (IsRanged ? "ranged" : "melee")
		};
		if (!context.State.IsSimulation)
		{
			(context.OnAttackResolved ?? ResolveAttackEffect.GlobalOnAttackResolved)?.Invoke(obj);
		}
		if (!flag4)
		{
			yield break;
		}
		if (!ZeroDamage)
		{
			CardDefinition cardDefinition = ((opponentCard.HasValue && context.CardDb != null) ? context.CardDb.GetDefinition(opponentCard.Value.DefinitionId) : null);
			bool banishOnDeath = BanishOnKill || ((object)cardDefinition != null && cardDefinition.Name.Equals("Shambling Horror", StringComparison.OrdinalIgnoreCase)) || (cardDefinition?.Name.Equals("Graveyard Librarian", StringComparison.OrdinalIgnoreCase) ?? false);
			yield return new DealDamageEvent
			{
				TargetInstanceId = targetId,
				Amount = 1 + num9,
				DamageType = (IsRanged ? "ranged" : "melee"),
				SourceInstanceId = context.SourceInstanceId,
				BanishOnDeath = banishOnDeath,
				Unpreventable = CannotReduceWounds
			};
		}
		if (StunOnHit)
		{
			yield return new ChangeReadyStateEvent
			{
				TargetInstanceId = targetId,
				NewReadyState = ReadyState.Stunned
			};
		}
		if (SpendOnHit)
		{
			yield return new ChangeReadyStateEvent
			{
				TargetInstanceId = targetId,
				NewReadyState = ReadyState.Spent
			};
		}
		if (PullOnHit)
		{
			int ownerPlayerIndex = defender.Value.OwnerPlayerIndex;
			int? num14 = BoardGeometry.FindCardRank(context.State.Players[ownerPlayerIndex], targetId);
			if (num14.HasValue && num14.Value > 1)
			{
				yield return new MarchEvent
				{
					PlayerIndex = ownerPlayerIndex,
					CardInstanceId = targetId,
					SourceRank = num14.Value,
					DestinationRank = num14.Value - 1
				};
			}
		}
		if (DestroyItemOnHit && defender.Value.AttachedItemIds.Count > 0)
		{
			int itemToDestroy = defender.Value.AttachedItemIds[0];
			CardLocator.FindAndMutate(context.State, targetId, delegate(ref CardInstance c)
			{
				c.AttachedItemIds.Remove(itemToDestroy);
			});
			CardInstance? destroyedItem = CardLocator.FindCard(context.State, itemToDestroy);
			int ownerIdx = destroyedItem?.OwnerPlayerIndex ?? defender.Value.OwnerPlayerIndex;
			PlayerState ownerState = context.State.Players[ownerIdx];
			foreach (PlayerState p in context.State.Players)
			{
				p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == itemToDestroy);
			}
			CardLocator.FindAndMutate(context.State, itemToDestroy, delegate(ref CardInstance c)
			{
				c.Zone = CardZone.Discard;
				c.AttachedToInstanceId = null;
			});
			if (destroyedItem.HasValue && !ownerState.DiscardPile.Any((CardInstance c) => c.InstanceId == itemToDestroy))
			{
				ownerState.DiscardPile.Add(destroyedItem.Value with
				{
					Zone = CardZone.Discard,
					AttachedToInstanceId = null
				});
			}
		}
	}
}
