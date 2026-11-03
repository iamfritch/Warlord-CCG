using System.Collections.Generic;
using System.Linq;

namespace WarlordEngine.Events;

/// <summary>Detaches an item from its bearer and moves it to its owner's discard pile.</summary>
public sealed record DestroyItemEvent : GameEvent
{
	public required int ItemInstanceId { get; init; }

	/// <summary>Bearer the item is attached to, if known.</summary>
	public int? HolderInstanceId { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		if (HolderInstanceId.HasValue)
		{
			CardLocator.FindAndMutate(state, HolderInstanceId.Value, delegate(ref CardInstance c)
			{
				c.AttachedItemIds.Remove(ItemInstanceId);
			});
		}
		CardInstance? item = CardLocator.FindCard(state, ItemInstanceId);
		foreach (PlayerState p in state.Players)
		{
			p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == ItemInstanceId);
		}
		CardLocator.FindAndMutate(state, ItemInstanceId, delegate(ref CardInstance c)
		{
			c.Zone = CardZone.Discard;
			c.AttachedToInstanceId = null;
		});
		if (item.HasValue)
		{
			PlayerState owner = state.Players[item.Value.OwnerPlayerIndex];
			if (!owner.DiscardPile.Any((CardInstance c) => c.InstanceId == ItemInstanceId))
			{
				owner.DiscardPile.Add(item.Value with
				{
					Zone = CardZone.Discard,
					AttachedToInstanceId = null
				});
			}
		}
		return state;
	}
}
