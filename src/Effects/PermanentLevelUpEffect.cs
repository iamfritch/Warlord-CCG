using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record PermanentLevelUpEffect(int LevelBonus = 1, int AtkBonus = 3, int AcBonus = 3, int SkillBonus = 2, int HpBonus = 1) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		CardLocator.FindAndMutate(context.State, instanceId, delegate(ref CardInstance c)
		{
			c.CurrentArmorClass += AcBonus;
			c.CurrentHitPoints += HpBonus;
			c.CurrentSkill += SkillBonus;
			c.BonusLevel += LevelBonus;
			c.SkillRatings["BonusLevel"] = (c.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0) + LevelBonus;
			c.SkillRatings["BonusAtk"] = (c.SkillRatings.TryGetValue("BonusAtk", out var value2) ? value2 : 0) + AtkBonus;
		});
		CardLocator.FindAndMutate(context.State, context.SourceInstanceId, delegate(ref CardInstance c)
		{
			c.Zone = CardZone.Banished;
		});
		yield break;
	}
}
