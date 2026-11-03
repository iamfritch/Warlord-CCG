using System.Collections.Generic;
using System.Linq;

namespace WarlordEngine.Events;

/// <summary>Detaches an item from its bearer and returns it to its owner's hand.</summary>
public sealed record SalvageItemEvent : GameEvent
{
	public required int ItemInstanceId { get; init; }

	public required int HolderInstanceId { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		CardLocator.FindAndMutate(state, HolderInstanceId, delegate(ref CardInstance c)
		{
			c.AttachedItemIds.Remove(ItemInstanceId);
		});
		CardLocator.FindAndMutate(state, ItemInstanceId, delegate(ref CardInstance c)
		{
			c.Zone = CardZone.Hand;
			c.AttachedToInstanceId = null;
		});
		CardInstance? item = CardLocator.FindCard(state, ItemInstanceId);
		if (item.HasValue)
		{
			PlayerState owner = state.Players[item.Value.OwnerPlayerIndex];
			owner.DiscardPile.RemoveAll((CardInstance c) => c.InstanceId == ItemInstanceId);
			foreach (PlayerState p in state.Players)
			{
				p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == ItemInstanceId);
			}
			if (!owner.Hand.Any((CardInstance c) => c.InstanceId == ItemInstanceId))
			{
				owner.Hand.Add(item.Value with
				{
					Zone = CardZone.Hand,
					AttachedToInstanceId = null
				});
			}
		}
		return state;
	}
}
