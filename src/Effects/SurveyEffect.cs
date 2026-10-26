using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record SurveyEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (cardInstance.HasValue && context.State.Players[cardInstance.Value.OwnerPlayerIndex].Deck.Count > 0)
		{
			GameEngine.AddTurnBuff(context.State, context.SourceInstanceId, "SURVEYED", 1);
		}
		yield break;
	}
}
