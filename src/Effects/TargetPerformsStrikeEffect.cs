using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record TargetPerformsStrikeEffect(int StrikeBonus = 0, bool IsRanged = false, bool BonusWoundAgainstEqualOrLowerLevel = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int? performerId = context.TargetInstanceId ?? (context.TargetInstanceIds != null && context.TargetInstanceIds.Count > 0 ? context.TargetInstanceIds[0] : (int?)null);
		if (!performerId.HasValue)
		{
			yield break;
		}
		int value = performerId.Value;
		CardInstance? cardInstance = context.FindCard(value);
		if (!cardInstance.HasValue)
		{
			yield break;
		}

		int? strikeTargetId = null;
		if (context.TargetInstanceIds != null && context.TargetInstanceIds.Count > 1)
		{
			strikeTargetId = context.TargetInstanceIds[1];
		}

		if (!strikeTargetId.HasValue)
		{
			// Look for strike target in range of performer
			int? pRank = BoardGeometry.FindCardRank(context.State.Players[cardInstance.Value.OwnerPlayerIndex], value);
			if (pRank.HasValue)
			{
				int oppIdx = 1 - cardInstance.Value.OwnerPlayerIndex;
				if (oppIdx >= 0 && oppIdx < context.State.Players.Count)
				{
					foreach (var (r, rCards) in context.State.Players[oppIdx].Ranks)
					{
						int d = BoardGeometry.GetCrossBoardDistance(pRank.Value, r);
						if (IsRanged ? BoardGeometry.IsRangedValid(pRank.Value, r, 2) : d <= 1)
						{
							if (rCards.Count > 0)
							{
								strikeTargetId = rCards[0].InstanceId;
								break;
							}
						}
					}
				}
				// If no opponent in range, must target friendly in range
				if (!strikeTargetId.HasValue)
				{
					var fPlayer = context.State.Players[cardInstance.Value.OwnerPlayerIndex];
					foreach (var (r, rCards) in fPlayer.Ranks)
					{
						int d = Math.Max(1, Math.Abs(pRank.Value - r));
						if (IsRanged ? d <= 2 : d <= 1)
						{
							var ally = rCards.FirstOrDefault(c => c.InstanceId != value);
							if (ally.InstanceId != 0)
							{
								strikeTargetId = ally.InstanceId;
								break;
							}
						}
					}
				}
			}
		}

		if (!strikeTargetId.HasValue)
		{
			yield break;
		}

		int instanceId = strikeTargetId.Value;
		CardInstance? cardInstance2 = context.FindCard(instanceId);
		if (!cardInstance2.HasValue)
		{
			yield break;
		}

		int num2 = 0;
		if (BonusWoundAgainstEqualOrLowerLevel)
		{
			CardDefinition? definition = context.CardDb.GetDefinition(cardInstance.Value.DefinitionId);
			CardDefinition? definition2 = context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId);
			int num3 = (definition?.Level ?? 0) + cardInstance.Value.BonusLevel;
			if ((definition2?.Level ?? 0) + cardInstance2.Value.BonusLevel <= num3)
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
