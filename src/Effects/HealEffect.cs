using System;
using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record HealEffect(int Amount = 1) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int num = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		CardInstance? cardInstance = context.FindCard(num);
		if (cardInstance.HasValue)
		{
			context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
			int effectiveMaxHitPoints = GameEngine.GetEffectiveMaxHitPoints(context.State, cardInstance.Value, context.CardDb);
			int val = ((cardInstance.Value.Wounds > 0) ? cardInstance.Value.Wounds : Math.Max(0, effectiveMaxHitPoints - cardInstance.Value.CurrentHitPoints));
			int num2 = Math.Min(Amount, val);
			if (num2 > 0)
			{
				yield return new ModifyStatEvent
				{
					TargetInstanceId = num,
					StatName = "CurrentHitPoints",
					Amount = num2
				};
			}
		}
	}
}
