using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record MoveRankEffect(int? DestinationRank = null, int? Delta = null) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int num = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(num);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int ownerPlayerIndex = cardInstance.Value.OwnerPlayerIndex;
		int? num2 = BoardGeometry.FindCardRank(context.State.Players[ownerPlayerIndex], num);
		if (num2.HasValue)
		{
			int val = DestinationRank ?? (num2.Value + Delta.GetValueOrDefault());
			val = Math.Max(1, val);
			if (num2.Value != val)
			{
				yield return new MarchEvent
				{
					PlayerIndex = ownerPlayerIndex,
					CardInstanceId = num,
					SourceRank = num2.Value,
					DestinationRank = val
				};
			}
		}
	}
}
