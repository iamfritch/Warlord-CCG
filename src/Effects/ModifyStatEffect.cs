using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ModifyStatEffect(string StatName, int Amount) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int targetInstanceId = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		yield return new ModifyStatEvent
		{
			TargetInstanceId = targetInstanceId,
			StatName = StatName,
			Amount = Amount
		};
	}
}
