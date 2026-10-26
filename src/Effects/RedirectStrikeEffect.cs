using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record RedirectStrikeEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		GameEngine.AddTurnBuff(context.State, context.SourceInstanceId, "REDIRECT_TO_ME", 1);
		yield break;
	}
}
