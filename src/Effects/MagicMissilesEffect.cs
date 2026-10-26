using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record MagicMissilesEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int level = GameEngine.GetEffectiveLevel(context.State, cardInstance.Value, context.CardDb);
		for (int i = 0; i < level; i++)
		{
			PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(0, IsRanged: true, 0, StunOnHit: false, SpendOnHit: false, PushOnHit: false, PullOnHit: false, DestroyItemOnHit: false, ZeroDamage: false, BonusFromLevel: false, TargetSpentOnly: false, TargetStunnedOnly: false, TargetSpentOrStunnedOnly: false, TargetLowerLevelOnly: false, BonusWoundAgainstHigherLevel: false, HasReach: false, TrueStrike: false, HasAdvantage: false, NoRollBonuses: true);
			foreach (GameEvent item in performStrikeEffect.Evaluate(context))
			{
				yield return item;
			}
		}
	}
}
