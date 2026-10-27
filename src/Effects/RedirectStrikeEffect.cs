using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record RedirectStrikeEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (context.TargetInstanceId.HasValue)
		{
			CardInstance? targetCard = context.FindCard(context.TargetInstanceId.Value);
			if (targetCard.HasValue)
			{
				CardDefinition? targetDef = context.CardDb?.GetDefinition(targetCard.Value.DefinitionId);
				bool targetHasTrueStrike = GameEngine.HasTurnBuff(context.State, targetCard.Value.InstanceId, "TRUE_STRIKE") ||
					(context.CardDb != null && GameEngine.GetContinuousPassiveBonuses(context.State, targetCard.Value, null, context.CardDb).TrueStrike) ||
					(targetDef != null && (targetDef.Traits.Any((string t) => t.Equals("True Strike", StringComparison.OrdinalIgnoreCase)) || targetDef.RulesText.Contains("True Strike", StringComparison.OrdinalIgnoreCase)));
				if (targetHasTrueStrike)
				{
					yield break;
				}
			}
		}
		GameEngine.AddTurnBuff(context.State, context.GetPerformerCharacterId(), "REDIRECT_TO_ME", 1);
		yield break;
	}
}
