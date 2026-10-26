using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record AnotherTavernBrawlEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int performerId = context.SourceInstanceId;
		CardInstance? performer = context.FindCard(performerId);
		if (!performer.HasValue)
		{
			yield break;
		}
		int num = 1 - performer.Value.OwnerPlayerIndex;
		if (num < 0 || num >= context.State.Players.Count)
		{
			yield break;
		}
		int? opponentCharId = context.TargetInstanceId;
		if (!opponentCharId.HasValue && context.State.Players[num].Ranks.TryGetValue(1, out List<CardInstance> value) && value.Count > 0)
		{
			opponentCharId = value[0].InstanceId;
		}
		if (!opponentCharId.HasValue)
		{
			yield break;
		}
		CardInstance? cardInstance = context.FindCard(opponentCharId.Value);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		CardDefinition definition = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0);
		EffectContext context2 = context with
		{
			SourceInstanceId = opponentCharId.Value,
			TargetInstanceId = performerId
		};
		foreach (GameEvent item in performStrikeEffect.Evaluate(context2))
		{
			yield return item;
		}
		CardDefinition definition2 = context.CardDb.GetDefinition(performer.Value.DefinitionId);
		PerformStrikeEffect performStrikeEffect2 = new PerformStrikeEffect(((object)definition2 != null && definition2.Attack?.Strikes.Count > 0) ? definition2.Attack.Strikes[0].Modifier : 0);
		EffectContext context3 = context with
		{
			SourceInstanceId = performerId,
			TargetInstanceId = opponentCharId.Value
		};
		foreach (GameEvent item2 in performStrikeEffect2.Evaluate(context3))
		{
			yield return item2;
		}
	}
}
