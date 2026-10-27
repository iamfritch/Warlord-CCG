using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record JourneyEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int movingCardId = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		CardInstance? cardInstance = context.FindCard(movingCardId);
		if (cardInstance.HasValue)
		{
			(int, int, int)? tuple = GameEngine.FindCharacterPosition(context.State, movingCardId);
			if (tuple.HasValue)
			{
				int playerIdx = tuple.Value.Item1;
				int currentRank = tuple.Value.Item2;
				int destRank = currentRank > 1 ? currentRank - 1 : currentRank + 1;
				yield return new CharacterMovedEvent
				{
					PlayerIndex = playerIdx,
					CardInstanceId = movingCardId,
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
				TargetInstanceId = movingCardId,
				NewReadyState = newReadyState
			};
		}
	}
}
