using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ReflexSaveEffect(int DC, IReadOnlyList<IEffect> FailureEffects, bool AddCasterSkill = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(instanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int num = DC;
		if (AddCasterSkill)
		{
			CardInstance? cardInstance2 = context.FindCard(context.SourceInstanceId);
			if (cardInstance2.HasValue)
			{
				num += GameEngine.GetEffectiveSkill(context.State, cardInstance2.Value, context.CardDb);
			}
		}
		int effectiveSkill = GameEngine.GetEffectiveSkill(context.State, cardInstance.Value, context.CardDb);
		int roll = context.RollDice(1, 20);
		bool success = roll == 20 || (roll != 1 && roll + effectiveSkill >= num);
		if (success)
		{
			yield break;
		}
		foreach (IEffect failureEffect in FailureEffects)
		{
			foreach (GameEvent item in failureEffect.Evaluate(context))
			{
				yield return item;
			}
		}
	}
}
