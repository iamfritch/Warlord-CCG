using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record JourneyEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int num = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(num);
		if (cardInstance.HasValue)
		{
			(int, int, int)? tuple = GameEngine.FindCharacterPosition(context.State, num);
			if (tuple.HasValue)
			{
				int playerIdx = tuple.Value.Item1;
				int currentRank = tuple.Value.Item2;
				int destRank = currentRank > 1 ? currentRank - 1 : currentRank + 1;
				yield return new CharacterMovedEvent
				{
					PlayerIndex = playerIdx,
					CardInstanceId = num,
					SourceRank = currentRank,
					DestinationRank = destRank
				};
			}
			ReadyState newReadyState = cardInstance.Value.ReadyState switch
			{
				ReadyState.Stunned => ReadyState.Spent,
				ReadyState.Spent => ReadyState.Ready,
				_ => ReadyState.Ready,
			};
			yield return new ChangeReadyStateEvent
			{
				TargetInstanceId = num,
				NewReadyState = newReadyState
			};
		}
	}
}
