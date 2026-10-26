using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record FortitudeSaveEffect(int DC, IReadOnlyList<IEffect> FailureEffects, bool PerEquippedItem = false, bool AddCasterSkill = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(instanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int effectiveDC = DC;
		if (AddCasterSkill)
		{
			CardInstance? cardInstance2 = context.FindCard(context.SourceInstanceId);
			if (cardInstance2.HasValue)
			{
				effectiveDC += GameEngine.GetEffectiveSkill(context.State, cardInstance2.Value, context.CardDb);
			}
		}
		int targetSkill = GameEngine.GetEffectiveSkill(context.State, cardInstance.Value, context.CardDb);
		int checks = ((!PerEquippedItem) ? 1 : cardInstance.Value.AttachedItemIds.Count);
		for (int i = 0; i < checks; i++)
		{
			if (context.RollDice(1, 20) + targetSkill >= effectiveDC)
			{
				continue;
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
}
