using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record WellSuppliedEffect(int CardsToReveal = 10) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		PlayerState playerState = context.State.Players[cardInstance.Value.OwnerPlayerIndex];
		int num = Math.Min(CardsToReveal, playerState.Deck.Count);
		if (num == 0)
		{
			yield break;
		}
		List<CardInstance> list = playerState.Deck.Take(num).ToList();
		playerState.Deck.RemoveRange(0, num);
		List<CardInstance> list2 = list.Where((CardInstance c) =>
		{
			CardDefinition definition = context.CardDb.GetDefinition(c.DefinitionId);
			return definition != null && definition.Type == CardType.Item;
		}).ToList();
		foreach (int item3 in (from c in playerState.Ranks.Values.SelectMany((List<CardInstance> r) => r)
			select c.InstanceId).ToList())
		{
			if (list2.Count == 0)
			{
				break;
			}
			for (int num2 = 0; num2 < list2.Count; num2++)
			{
				CardInstance item = list2[num2];
				if (context.CardDb.GetDefinition(item.DefinitionId) != null)
				{
					list2.RemoveAt(num2);
					list.RemoveAll((CardInstance c) => c.InstanceId == item.InstanceId);
					CardLocator.FindAndMutate(context.State, item3, delegate(ref CardInstance c)
					{
						c.AttachedItemIds.Add(item.InstanceId);
					});
					CardInstance item2 = item with
					{
						Zone = CardZone.Attached,
						AttachedToInstanceId = item3
					};
					playerState.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == item.InstanceId);
					playerState.DiscardPile.Add(item2);
					break;
				}
			}
		}
		playerState.Deck.AddRange(list);
		for (int num3 = playerState.Deck.Count - 1; num3 > 0; num3--)
		{
			int num4 = Random.Shared.Next(num3 + 1);
			List<CardInstance> deck = playerState.Deck;
			int index = num3;
			List<CardInstance> deck2 = playerState.Deck;
			int index2 = num4;
			CardInstance value = playerState.Deck[num4];
			CardInstance value2 = playerState.Deck[num3];
			deck[index] = value;
			deck2[index2] = value2;
		}
	}
}
