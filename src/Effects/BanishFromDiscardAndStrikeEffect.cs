using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record BanishFromDiscardAndStrikeEffect(int StrikeBonus = 3, int MaxCards = 2) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int banishedCount = 0;
		foreach (PlayerState player in context.State.Players)
		{
			for (; banishedCount < MaxCards; banishedCount++)
			{
				if (player.DiscardPile.Count <= 0)
				{
					break;
				}
				int num = player.DiscardPile.FindIndex((CardInstance c) =>
				{
					CardDefinition definition = context.CardDb.GetDefinition(c.DefinitionId);
					return definition != null && definition.Type == CardType.Character;
				});
				if (num < 0)
				{
					num = 0;
				}
				CardInstance cardInstance = player.DiscardPile[num];
				player.DiscardPile.RemoveAt(num);
				CardLocator.FindAndMutate(context.State, cardInstance.InstanceId, delegate(ref CardInstance c)
				{
					c.Zone = CardZone.Banished;
				});
			}
			if (banishedCount >= MaxCards)
			{
				break;
			}
		}
		if (banishedCount == 0)
		{
			yield break;
		}
		for (int i = 0; i < banishedCount; i++)
		{
			PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(StrikeBonus, IsRanged: true);
			foreach (GameEvent item in performStrikeEffect.Evaluate(context))
			{
				yield return item;
			}
		}
	}
}
