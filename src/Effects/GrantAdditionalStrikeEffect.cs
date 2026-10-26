using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record GrantAdditionalStrikeEffect(int Modifier = 0, bool IsRanged = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		GameEngine.AddTurnBuff(context.State, instanceId, "EXTRA_STRIKE", Modifier);
		yield break;
	}
}
