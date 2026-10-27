using System.Collections.Generic;

namespace WarlordEngine.Events;

public static class CardLocator
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
			foreach (CardInstance itemAttached in player.AttachedItems)
			{
				if (itemAttached.InstanceId == instanceId)
				{
					return itemAttached;
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
			if (MutateInList(player.AttachedItems, instanceId, mutator))
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

	public static int ResolvePerformerCharacterId(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		for (int i = 0; i < state.Players.Count; i++)
		{
			foreach (var (_, rankCards) in state.Players[i].Ranks)
			{
				for (int j = 0; j < rankCards.Count; j++)
				{
					if (rankCards[j].InstanceId == instanceId)
					{
						return instanceId;
					}
				}
			}
		}

		CardInstance? card = FindCard(state, instanceId);
		if (card.HasValue)
		{
			int? bearerId = card.Value.AttachedToInstanceId ?? card.Value.AttachedToId;
			if (bearerId.HasValue)
			{
				for (int i = 0; i < state.Players.Count; i++)
				{
					foreach (var (_, rankCards) in state.Players[i].Ranks)
					{
						for (int j = 0; j < rankCards.Count; j++)
						{
							if (rankCards[j].InstanceId == bearerId.Value)
							{
								return bearerId.Value;
							}
						}
					}
				}
			}
		}

		for (int i = 0; i < state.Players.Count; i++)
		{
			foreach (var (_, rankCards) in state.Players[i].Ranks)
			{
				for (int j = 0; j < rankCards.Count; j++)
				{
					if (rankCards[j].AttachedItemIds != null && rankCards[j].AttachedItemIds.Contains(instanceId))
					{
						return rankCards[j].InstanceId;
					}
				}
			}
		}

		return instanceId;
	}

	public static CardInstance? ResolvePerformerCharacter(GameState state, int instanceId, CardDatabase? cardDb = null)
	{
		int bearerId = ResolvePerformerCharacterId(state, instanceId, cardDb);
		return FindCard(state, bearerId);
	}
}
