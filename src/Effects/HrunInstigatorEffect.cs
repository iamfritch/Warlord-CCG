using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record HrunInstigatorEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (context.TargetInstanceId.HasValue)
		{
			PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect();
			EffectContext context2 = context with
			{
				SourceInstanceId = context.TargetInstanceId.Value,
				TargetInstanceId = context.SourceInstanceId
			};
			foreach (GameEvent item in performStrikeEffect.Evaluate(context2))
			{
				yield return item;
			}
		}
		PerformStrikeEffect performStrikeEffect2 = new PerformStrikeEffect(0, IsRanged: false, 0, StunOnHit: false, SpendOnHit: false, PushOnHit: false, PullOnHit: false, DestroyItemOnHit: false, ZeroDamage: false, BonusFromLevel: false, TargetSpentOnly: false, TargetStunnedOnly: false, TargetSpentOrStunnedOnly: false, TargetLowerLevelOnly: false, BonusWoundAgainstHigherLevel: false, HasReach: false, TrueStrike: false, HasAdvantage: true);
		foreach (GameEvent item2 in performStrikeEffect2.Evaluate(context))
		{
			yield return item2;
		}
	}
}
