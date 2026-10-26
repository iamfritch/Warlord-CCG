using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record SalvageEquippedItemEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(instanceId);
		if (!cardInstance.HasValue || cardInstance.Value.AttachedItemIds.Count == 0)
		{
			yield break;
		}
		int itemToSalvage = -1;
		foreach (int attachedItemId in cardInstance.Value.AttachedItemIds)
		{
			CardInstance? cardInstance2 = context.FindCard(attachedItemId);
			if (cardInstance2.HasValue)
			{
				CardDefinition definition = context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId);
				if (definition != null && (definition.Type == CardType.Item || definition.Traits.Any((string t) => t.Equals("Armor", StringComparison.OrdinalIgnoreCase) || t.Equals("Shield", StringComparison.OrdinalIgnoreCase) || t.Equals("Weapon", StringComparison.OrdinalIgnoreCase))))
				{
					itemToSalvage = attachedItemId;
					break;
				}
			}
		}
		if (itemToSalvage < 0)
		{
			itemToSalvage = cardInstance.Value.AttachedItemIds[0];
		}
		CardLocator.FindAndMutate(context.State, instanceId, delegate(ref CardInstance c)
		{
			c.AttachedItemIds.Remove(itemToSalvage);
		});
		CardLocator.FindAndMutate(context.State, itemToSalvage, delegate(ref CardInstance c)
		{
			c.Zone = CardZone.Hand;
			c.AttachedToInstanceId = null;
		});
		CardInstance? cardInstance3 = context.FindCard(itemToSalvage);
		if (cardInstance3.HasValue)
		{
			PlayerState playerState = context.State.Players[cardInstance3.Value.OwnerPlayerIndex];
			playerState.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == itemToSalvage);
			foreach (PlayerState p in context.State.Players)
			{
				p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == itemToSalvage);
			}
			if (!playerState.Hand.Any((CardInstance c) => c.InstanceId == itemToSalvage))
			{
				playerState.Hand.Add(cardInstance3.Value with
				{
					Zone = CardZone.Hand,
					AttachedToInstanceId = null
				});
			}
		}
	}
}
