using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record PhaseChangeEvent : GameEvent
{
	public required TurnPhase FromPhase { get; init; }

	public required TurnPhase ToPhase { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		state.CurrentPhase = ToPhase;
		return state;
	}

}
