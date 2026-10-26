using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record TargetHigherLevelDeverenianJourneyEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int value = context.TargetInstanceId.Value;
		if (value == context.SourceInstanceId)
		{
			yield break;
		}
		CardInstance? cardInstance = context.FindCard(value);
		CardInstance? cardInstance2 = context.FindCard(context.SourceInstanceId);
		if (!cardInstance.HasValue || !cardInstance2.HasValue)
		{
			yield break;
		}
		CardDefinition? definition = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
		int effectiveLevel = GameEngine.GetEffectiveLevel(context.State, cardInstance.Value, context.CardDb);
		int effectiveLevel2 = GameEngine.GetEffectiveLevel(context.State, cardInstance2.Value, context.CardDb);
		if (!GameEngine.IsDeverenian(definition) || effectiveLevel <= effectiveLevel2)
		{
			yield break;
		}
		JourneyEffect journeyEffect = new JourneyEffect();
		foreach (GameEvent item in journeyEffect.Evaluate(context))
		{
			yield return item;
		}
	}
}
