using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ResolveAttackEffect : IEffect
{
	public static Action<AttackResolutionDetails>? GlobalOnAttackResolved { get; set; }

	public static Action<AttackResolutionDetails>? OnAttackResolved
	{
		get
		{
			return GlobalOnAttackResolved;
		}
		set
		{
			GlobalOnAttackResolved = value;
		}
	}

	public Action<AttackResolutionDetails>? InstanceOnAttackResolved { get; init; }

	public required int StrikeBonus { get; init; }

	public string DamageType { get; init; } = "melee";

	public int Damage { get; init; } = 1;

	public int AdditionalWounds { get; init; }

	public bool EnableCriticals { get; init; }

	public bool CannotReduceWounds { get; init; }

	public bool BanishOnKill { get; init; }

	public bool TrueStrike { get; init; }

	public bool HasAdvantage { get; init; }

	public bool HasDisadvantage { get; init; }

	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int value = context.TargetInstanceId.Value;
		CardInstance? cardInstance = context.FindCard(value);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int num = cardInstance.Value.CurrentArmorClass;
		if (cardInstance.Value.AttachedItemIds.Count > 0)
		{
			int num2 = 0;
			foreach (int attachedItemId in cardInstance.Value.AttachedItemIds)
			{
				CardInstance? cardInstance2 = context.FindCard(attachedItemId);
				if (cardInstance2.HasValue)
				{
					CardDefinition definition = context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId);
					if (definition != null)
					{
						num2 += GameEngine.GetItemPassiveStats(definition).Ac;
					}
				}
			}
			CardDefinition definition2 = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
			if ((object)definition2 != null && definition2.ArmorClass.HasValue && num == definition2.ArmorClass.Value)
			{
				num += num2;
			}
		}
		CombatContext combatCtx = new CombatContext
		{
			IsDefending = true,
			OpponentCard = context.FindCard(context.SourceInstanceId),
			IsMelee = DamageType.Equals("melee", StringComparison.OrdinalIgnoreCase),
			IsRanged = DamageType.Equals("ranged", StringComparison.OrdinalIgnoreCase)
		};
		PassiveBonuses continuousPassiveBonuses = GameEngine.GetContinuousPassiveBonuses(context.State, cardInstance.Value, combatCtx, context.CardDb);
		num += continuousPassiveBonuses.Ac;
		bool hasAdv = HasAdvantage || (context.SourceInstanceId != 0 && GameEngine.HasTurnBuff(context.State, context.SourceInstanceId, "ADVANTAGE"));
		bool hasDis = HasDisadvantage || (context.SourceInstanceId != 0 && GameEngine.HasTurnBuff(context.State, context.SourceInstanceId, "DISADVANTAGE"));
		int num3 = context.RollDice(1, 20);
		if (hasAdv && !hasDis)
		{
			int roll2 = context.RollDice(1, 20);
			num3 = Math.Max(num3, roll2);
		}
		else if (hasDis && !hasAdv)
		{
			int roll2 = context.RollDice(1, 20);
			num3 = Math.Min(num3, roll2);
		}
		bool flag = EnableCriticals && num3 == 1;
		bool flag2 = EnableCriticals && num3 == 20;
		int num4 = num3 + StrikeBonus;
		bool flag3 = !flag && (flag2 || num4 >= num);
		int num5 = 0;
		if (flag3)
		{
			num5 = ((Damage < 0) ? 1 : Damage) + AdditionalWounds;
		}
		CardInstance? cardInstance3 = context.FindCard(context.SourceInstanceId);
		string attackerName = ((!cardInstance3.HasValue) ? $"Card #{context.SourceInstanceId}" : (context.CardDb.GetDefinition(cardInstance3.Value.DefinitionId)?.Name ?? $"Card #{context.SourceInstanceId}"));
		string defenderName = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId)?.Name ?? $"Card #{value}";
		AttackResolutionDetails obj = new AttackResolutionDetails
		{
			AttackerInstanceId = context.SourceInstanceId,
			DefenderInstanceId = value,
			AttackerName = attackerName,
			DefenderName = defenderName,
			Roll = num3,
			StrikeBonus = StrikeBonus,
			DefenderArmorClass = num,
			IsCritical = flag2,
			IsCriticalFailure = flag,
			IsHit = flag3,
			DamageDealt = num5,
			DamageType = DamageType
		};
		if (InstanceOnAttackResolved != null)
		{
			InstanceOnAttackResolved(obj);
		}
		else if (!context.State.IsSimulation)
		{
			(context.OnAttackResolved ?? GlobalOnAttackResolved)?.Invoke(obj);
		}
		if (flag3)
		{
			CardDefinition cardDefinition = (cardInstance3.HasValue ? context.CardDb.GetDefinition(cardInstance3.Value.DefinitionId) : null);
			bool banishOnDeath = BanishOnKill || (cardDefinition?.Name.Equals("Shambling Horror", StringComparison.OrdinalIgnoreCase) ?? false);
			yield return new DealDamageEvent
			{
				TargetInstanceId = value,
				Amount = num5,
				DamageType = DamageType,
				SourceInstanceId = context.SourceInstanceId,
				BanishOnDeath = banishOnDeath,
				Unpreventable = CannotReduceWounds
			};
		}
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ResolveAttackEffect(ResolveAttackEffect original)
	{
		InstanceOnAttackResolved = original.InstanceOnAttackResolved;
		StrikeBonus = original.StrikeBonus;
		DamageType = original.DamageType;
		Damage = original.Damage;
		AdditionalWounds = original.AdditionalWounds;
		EnableCriticals = original.EnableCriticals;
		CannotReduceWounds = original.CannotReduceWounds;
		BanishOnKill = original.BanishOnKill;
		TrueStrike = original.TrueStrike;
	}
}
