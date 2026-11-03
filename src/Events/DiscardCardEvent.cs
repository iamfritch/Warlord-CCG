namespace WarlordEngine.Events;

public sealed record DiscardCardEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }

	public DiscardCardEvent()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public DiscardCardEvent(int playerIndex, int cardInstanceId)
	{
		PlayerIndex = playerIndex;
		CardInstanceId = cardInstanceId;
	}

	public override GameState ApplyToState(GameState state)
	{
		if (PlayerIndex >= 0 && PlayerIndex < state.Players.Count)
		{
			PlayerState player = state.Players[PlayerIndex];
			int idx = player.Hand.FindIndex((CardInstance c) => c.InstanceId == CardInstanceId);
			if (idx >= 0)
			{
				CardInstance card = player.Hand[idx];
				player.Hand.RemoveAt(idx);
				player.DiscardPile.Add(card with { Zone = CardZone.Discard });
			}
		}
		return state;
	}
}
