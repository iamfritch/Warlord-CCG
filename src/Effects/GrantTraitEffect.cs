using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record GrantTraitEffect(string TraitName, int Value = 1) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int instanceId = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		GameEngine.AddTurnBuff(context.State, instanceId, TraitName.ToUpperInvariant(), Value);
		yield break;
	}
}
