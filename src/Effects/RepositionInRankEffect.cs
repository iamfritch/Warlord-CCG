using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record RepositionInRankEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		(int, int, int)? tuple = GameEngine.FindCharacterPosition(context.State, instanceId);
		if (tuple.HasValue)
		{
			List<CardInstance> list = context.State.Players[tuple.Value.Item1].Ranks[tuple.Value.Item2];
			if (list.Count > 1)
			{
				int newSlot = (tuple.Value.Item3 == 0) ? list.Count - 1 : 0;
				yield return new CharacterMovedEvent
				{
					PlayerIndex = tuple.Value.Item1,
					CardInstanceId = instanceId,
					SourceRank = tuple.Value.Item2,
					DestinationRank = tuple.Value.Item2,
					TargetSlotIndex = newSlot
				};
			}
		}
	}
}
