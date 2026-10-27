using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record StrongarmStoutEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int cardsDrawn = 0;
		foreach (PlayerState player in context.State.Players)
		{
			if (player.Deck.Count > 0)
			{
				CardInstance cardInstance = player.Deck[0];
				player.Deck.RemoveAt(0);
				player.Hand.Add(cardInstance with
				{
					Zone = CardZone.Hand
				});
				cardsDrawn++;
			}
		}
		int targetId = context.TargetInstanceId ?? context.SourceInstanceId;
		int characterId = (targetId == context.SourceInstanceId)
			? context.GetPerformerCharacterId()
			: context.GetPerformerCharacterId(targetId);

		if (cardsDrawn > 0)
		{
			GameEngine.AddTurnBuff(context.State, characterId, "ATK", 2 * cardsDrawn);
			GameEngine.AddTurnBuff(context.State, characterId, "HP", cardsDrawn);
			CardLocator.FindAndMutate(context.State, characterId, delegate(ref CardInstance c)
			{
				c.CurrentHitPoints += cardsDrawn;
			});
		}
		yield break;
	}
}
