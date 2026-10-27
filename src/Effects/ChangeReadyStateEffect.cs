using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ChangeReadyStateEffect(ReadyState TargetState, bool OnlyIfStunned = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int num = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		CardInstance? cardInstance = context.FindCard(num);
		if (cardInstance.HasValue && (!OnlyIfStunned || cardInstance.Value.ReadyState == ReadyState.Stunned))
		{
			yield return new ChangeReadyStateEvent
			{
				TargetInstanceId = num,
				NewReadyState = TargetState
			};
		}
	}
}
