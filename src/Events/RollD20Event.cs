using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record RollD20Event : GameEvent
{
	public required int SourceInstanceId { get; init; }

	public required int TargetDC { get; init; }

	public required int Bonus { get; init; }

	public int? RollResult { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		return state;
	}

}
