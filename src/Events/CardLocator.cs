using System.Collections.Generic;

namespace WarlordEngine.Events;

internal static class CardLocator
{
	public delegate void CardMutator(ref CardInstance card);

	public static CardInstance? FindCard(GameState state, int instanceId)
	{
		foreach (PlayerState player in state.Players)
		{
			foreach (CardInstance item in player.Deck)
			{
				if (item.InstanceId == instanceId)
				{
					return item;
				}
			}
			foreach (CardInstance item2 in player.Hand)
			{
				if (item2.InstanceId == instanceId)
				{
					return item2;
				}
			}
			foreach (CardInstance item3 in player.DiscardPile)
			{
				if (item3.InstanceId == instanceId)
				{
					return item3;
				}
			}
			foreach (CardInstance item4 in player.BanishPile)
			{
				if (item4.InstanceId == instanceId)
				{
					return item4;
				}
			}
			foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
			{
				rank.Deconstruct(out var _, out var value);
				foreach (CardInstance item5 in value)
				{
					if (item5.InstanceId == instanceId)
					{
						return item5;
					}
				}
			}
		}
		return null;
	}

	public static bool FindAndMutate(GameState state, int instanceId, CardMutator mutator)
	{
		foreach (PlayerState player in state.Players)
		{
			if (MutateInList(player.Deck, instanceId, mutator))
			{
				return true;
			}
			if (MutateInList(player.Hand, instanceId, mutator))
			{
				return true;
			}
			if (MutateInList(player.DiscardPile, instanceId, mutator))
			{
				return true;
			}
			if (MutateInList(player.BanishPile, instanceId, mutator))
			{
				return true;
			}
			foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
			{
				rank.Deconstruct(out var _, out var value);
				if (MutateInList(value, instanceId, mutator))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool MutateInList(List<CardInstance> cards, int instanceId, CardMutator mutator)
	{
		for (int i = 0; i < cards.Count; i++)
		{
			if (cards[i].InstanceId == instanceId)
			{
				CardInstance card = cards[i];
				mutator(ref card);
				cards[i] = card;
				return true;
			}
		}
		return false;
	}
}
