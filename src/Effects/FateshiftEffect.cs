using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record FateshiftEffect(int PreventWounds = 1) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int num = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (cardInstance.HasValue)
		{
			PlayerState playerState = context.State.Players[cardInstance.Value.OwnerPlayerIndex];
			if (playerState.Deck.Count > 0)
			{
				CardInstance cardInstance2 = playerState.Deck[0];
				int value = context.CardDb.GetDefinition(cardInstance2.DefinitionId)?.Level ?? 0;
				GameEngine.AddTurnBuff(context.State, num, "AC", value);
			}
		}
		yield return new PreventWoundsEvent
		{
			TargetInstanceId = num,
			Amount = PreventWounds
		};
	}
}
