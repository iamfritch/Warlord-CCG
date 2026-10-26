using System.Collections.Generic;
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
