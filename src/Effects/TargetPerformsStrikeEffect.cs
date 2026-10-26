using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record TargetPerformsStrikeEffect(int StrikeBonus = 0, bool IsRanged = false, bool BonusWoundAgainstEqualOrLowerLevel = false) : IEffect
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
		int num = 1 - cardInstance.Value.OwnerPlayerIndex;
		if (num < 0 || num >= context.State.Players.Count || !context.State.Players[num].Ranks.TryGetValue(1, out List<CardInstance> value2) || value2.Count == 0)
		{
			yield break;
		}
		int instanceId = value2[0].InstanceId;
		CardInstance cardInstance2 = value2[0];
		int num2 = 0;
		if (BonusWoundAgainstEqualOrLowerLevel)
		{
			CardDefinition? definition = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
			CardDefinition definition2 = context.CardDb.GetDefinition(cardInstance2.DefinitionId);
			int num3 = (definition?.Level ?? 0) + cardInstance.Value.BonusLevel;
			if ((definition2?.Level ?? 0) + cardInstance2.BonusLevel <= num3)
			{
				num2++;
			}
		}
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(StrikeBonus, IsRanged, num2);
		EffectContext context2 = context with
		{
			SourceInstanceId = value,
			TargetInstanceId = instanceId
		};
		foreach (GameEvent item in performStrikeEffect.Evaluate(context2))
		{
			yield return item;
		}
	}
}
