using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record TheMightyMuggEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int targetId = context.TargetInstanceId.Value;
		CardInstance? cardInstance = context.FindCard(targetId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int ownerPlayerIndex = cardInstance.Value.OwnerPlayerIndex;
		int? num = BoardGeometry.FindCardRank(context.State.Players[ownerPlayerIndex], targetId);
		if (num.HasValue && num.Value > 1)
		{
			int destinationRank = Math.Max(1, num.Value - 2);
			yield return new MarchEvent
			{
				PlayerIndex = ownerPlayerIndex,
				CardInstanceId = targetId,
				SourceRank = num.Value,
				DestinationRank = destinationRank
			};
		}
		yield return new DealDamageEvent
		{
			TargetInstanceId = targetId,
			Amount = 1,
			DamageType = "physical"
		};
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect();
		EffectContext context2 = context with
		{
			SourceInstanceId = targetId,
			TargetInstanceId = null
		};
		foreach (GameEvent item in performStrikeEffect.Evaluate(context2))
		{
			yield return item;
		}
	}
}
