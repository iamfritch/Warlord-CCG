using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ChangeReadyStateEffect(ReadyState TargetState, bool OnlyIfStunned = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int num = context.TargetInstanceId ?? context.SourceInstanceId;
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
