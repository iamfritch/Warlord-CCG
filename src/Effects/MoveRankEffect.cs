using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record MoveRankEffect(int? DestinationRank = null, int? Delta = null) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int movingCardId = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		CardInstance? cardInstance = context.FindCard(movingCardId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int ownerPlayerIndex = cardInstance.Value.OwnerPlayerIndex;
		int? currentRank = BoardGeometry.FindCardRank(context.State.Players[ownerPlayerIndex], movingCardId);
		if (currentRank.HasValue)
		{
			int val = DestinationRank ?? (currentRank.Value + Delta.GetValueOrDefault());
			val = Math.Max(1, val);
			if (currentRank.Value != val)
			{
				yield return new MarchEvent
				{
					PlayerIndex = ownerPlayerIndex,
					CardInstanceId = movingCardId,
					SourceRank = currentRank.Value,
					DestinationRank = val
				};
			}
		}
	}
}
