using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record PackTacticsEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int attackerId = context.TargetInstanceId.Value;
		CardInstance? attacker = context.FindCard(attackerId);
		if (!attacker.HasValue)
		{
			yield break;
		}
		yield return new ChangeReadyStateEvent
		{
			TargetInstanceId = attackerId,
			NewReadyState = ReadyState.Spent
		};
		int num = 1 - attacker.Value.OwnerPlayerIndex;
		if (num < 0 || num >= context.State.Players.Count || !context.State.Players[num].Ranks.TryGetValue(1, out List<CardInstance> value) || value.Count <= 0)
		{
			yield break;
		}
		int instanceId = value[0].InstanceId;
		CardDefinition definition = context.CardDb.GetDefinition(attacker.Value.DefinitionId);
		int strikeBonus = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0);
		bool isRanged = (object)definition != null && definition.Attack?.Strikes.Count > 0 && definition.Attack.Strikes[0].IsRanged;
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(strikeBonus, isRanged);
		EffectContext context2 = context with
		{
			SourceInstanceId = attackerId,
			TargetInstanceId = instanceId
		};
		foreach (GameEvent item in performStrikeEffect.Evaluate(context2))
		{
			yield return item;
		}
	}
}
