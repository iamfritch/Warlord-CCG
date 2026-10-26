using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record DealDamageEvent : GameEvent
{
	public required int TargetInstanceId { get; init; }

	public required int Amount { get; init; }

	public required string DamageType { get; init; }

	public int? SourceInstanceId { get; init; }

	public bool BanishOnDeath { get; init; }

	public bool Unpreventable { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		int damage = Amount;
		CardLocator.FindAndMutate(state, TargetInstanceId, delegate(ref CardInstance card)
		{
			if (!Unpreventable && card.StatusTags != null)
			{
				for (int num = card.StatusTags.Count - 1; num >= 0; num--)
				{
					if (card.StatusTags[num].StartsWith("prevent_wounds:") && int.TryParse(card.StatusTags[num].Substring("prevent_wounds:".Length), out var result))
					{
						int num2 = Math.Min(damage, result);
						damage -= num2;
						card.StatusTags.RemoveAt(num);
						int num3 = result - num2;
						if (num3 > 0)
						{
							card.StatusTags.Add($"prevent_wounds:{num3}");
						}
					}
				}
			}
			card.Wounds += damage;
			card.CurrentHitPoints -= damage;
		});
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DealDamageEvent(DealDamageEvent original)
		: base(original)
	{
		TargetInstanceId = original.TargetInstanceId;
		Amount = original.Amount;
		DamageType = original.DamageType;
		SourceInstanceId = original.SourceInstanceId;
		BanishOnDeath = original.BanishOnDeath;
		Unpreventable = original.Unpreventable;
	}
}
