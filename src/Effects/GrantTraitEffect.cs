using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record GrantTraitEffect(string TraitName, int Value = 1) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		GameEngine.AddTurnBuff(context.State, instanceId, TraitName.ToUpperInvariant(), Value);
		yield break;
	}
}
