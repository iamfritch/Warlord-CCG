using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record DrawCardEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		PlayerState playerState = state.Players[PlayerIndex];
		if (playerState.Deck.Count == 0)
		{
			return state;
		}
		CardInstance cardInstance = playerState.Deck[0];
		playerState.Deck.RemoveAt(0);
		CardInstance item = cardInstance with
		{
			Zone = CardZone.Hand
		};
		playerState.Hand.Add(item);
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DrawCardEvent(DrawCardEvent original)
		: base(original)
	{
		PlayerIndex = original.PlayerIndex;
	}
}
