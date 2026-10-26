using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record ShadowBoltsEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int value = context.TargetInstanceId.Value;
		CardInstance? cardInstance = context.FindCard(value);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		CardInstance? cardInstance2 = context.FindCard(context.SourceInstanceId);
		CardDefinition cardDefinition = (cardInstance2.HasValue ? context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId) : null);
		bool num = cardDefinition != null && ((cardDefinition.Class?.ToString().Contains("Rogue", StringComparison.OrdinalIgnoreCase) ?? false) || cardDefinition.Traits.Any((string t) => t.Contains("Rogue", StringComparison.OrdinalIgnoreCase)));
		bool isWizard = cardDefinition != null && ((cardDefinition.Class?.ToString().Contains("Wizard", StringComparison.OrdinalIgnoreCase) ?? false) || cardDefinition.Traits.Any((string t) => t.Contains("Wizard", StringComparison.OrdinalIgnoreCase)));
		int additionalWounds = ((num && cardInstance.Value.ReadyState == ReadyState.Spent) ? 1 : 0);
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(4, IsRanged: true, additionalWounds);
		foreach (GameEvent item in performStrikeEffect.Evaluate(context))
		{
			yield return item;
		}
		if (!isWizard)
		{
			yield break;
		}
		PerformStrikeEffect performStrikeEffect2 = new PerformStrikeEffect(4, IsRanged: true);
		foreach (GameEvent item2 in performStrikeEffect2.Evaluate(context))
		{
			yield return item2;
		}
	}
}
