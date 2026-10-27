using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record PreventWoundsEffect(int Amount = 1, bool WoundSelf = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (WoundSelf)
		{
			yield return new DealDamageEvent
			{
				TargetInstanceId = context.GetPerformerCharacterId(),
				Amount = 1,
				DamageType = "physical"
			};
		}
		if (context.TargetInstanceId.HasValue)
		{
			yield return new PreventWoundsEvent
			{
				TargetInstanceId = context.TargetInstanceId.Value,
				Amount = Amount
			};
		}
	}
}
