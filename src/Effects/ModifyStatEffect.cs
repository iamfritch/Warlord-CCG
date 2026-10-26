using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ModifyStatEffect(string StatName, int Amount) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetInstanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		yield return new ModifyStatEvent
		{
			TargetInstanceId = targetInstanceId,
			StatName = StatName,
			Amount = Amount
		};
	}
}
