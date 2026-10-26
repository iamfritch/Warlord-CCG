using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record DealDamageEffect(int Amount, string DamageType) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (context.TargetInstanceId.HasValue)
		{
			yield return new DealDamageEvent
			{
				TargetInstanceId = context.TargetInstanceId.Value,
				Amount = Amount,
				DamageType = DamageType
			};
		}
	}
}
