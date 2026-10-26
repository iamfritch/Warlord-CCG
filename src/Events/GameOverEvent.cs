using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record GameOverEvent : GameEvent
{
	public required int? WinningPlayerIndex { get; init; }

	public required GameOverReason Reason { get; init; }

	public int? DefeatedPlayerIndex { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		state.IsGameOver = true;
		state.WinningPlayerIndex = WinningPlayerIndex;
		state.GameOverReason = Reason;
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private GameOverEvent(GameOverEvent original)
		: base(original)
	{
		WinningPlayerIndex = original.WinningPlayerIndex;
		Reason = original.Reason;
		DefeatedPlayerIndex = original.DefeatedPlayerIndex;
	}
}
