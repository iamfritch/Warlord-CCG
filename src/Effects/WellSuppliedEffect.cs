using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Actions;
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
		List<CardInstance> revealed = playerState.Deck.Take(num).ToList();
		playerState.Deck.RemoveRange(0, num);
		List<CardInstance> revealedItems = revealed.Where((CardInstance c) =>
		{
			CardDefinition definition = context.CardDb.GetDefinition(c.DefinitionId);
			return definition != null && definition.Type == CardType.Item;
		}).ToList();
		foreach (int characterId in (from c in playerState.Ranks.Values.SelectMany((List<CardInstance> r) => r)
			select c.InstanceId).ToList())
		{
			if (revealedItems.Count == 0)
			{
				break;
			}
			// Each character may equip at most one revealed item, and only one it may legally equip.
			CardInstance? character = playerState.Ranks.Values.SelectMany((List<CardInstance> r) => r)
				.Where((CardInstance c) => c.InstanceId == characterId)
				.Select((CardInstance c) => (CardInstance?)c)
				.FirstOrDefault();
			CardDefinition characterDef = character.HasValue ? context.CardDb.GetDefinition(character.Value.DefinitionId) : null;
			if (characterDef == null)
			{
				continue;
			}
			for (int idx = 0; idx < revealedItems.Count; idx++)
			{
				CardInstance item = revealedItems[idx];
				CardDefinition itemDef = context.CardDb.GetDefinition(item.DefinitionId);
				if (itemDef == null || !PlayCardAction.ValidateItemEquip(itemDef, characterDef, character.Value, context.State, context.CardDb).IsSuccess)
				{
					continue;
				}
				revealedItems.RemoveAt(idx);
				revealed.RemoveAll((CardInstance c) => c.InstanceId == item.InstanceId);
				CardLocator.FindAndMutate(context.State, characterId, delegate(ref CardInstance c)
				{
					c.AttachedItemIds.Add(item.InstanceId);
				});
				CardInstance attached = item with
				{
					Zone = CardZone.Attached,
					AttachedToInstanceId = characterId
				};
				playerState.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == item.InstanceId);
				playerState.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == item.InstanceId);
				playerState.AttachedItems.Add(attached);
				break;
			}
		}
		playerState.Deck.AddRange(revealed);
		for (int i = playerState.Deck.Count - 1; i > 0; i--)
		{
			int j = Math.Abs(context.RollDice(0, i)) % (i + 1);
			(playerState.Deck[i], playerState.Deck[j]) = (playerState.Deck[j], playerState.Deck[i]);
		}
	}
}
