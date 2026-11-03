using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using WarlordEngine.Abilities;

namespace WarlordEngine.Events;

public sealed record AbilityActivatedEvent : GameEvent
{
	public required int SourceInstanceId { get; init; }

	public int? PerformerInstanceId { get; init; }

	public required int AbilityIndex { get; init; }

	public required int[] TargetInstanceIds { get; init; }

	public required AbilityDefinition Ability { get; init; }

	public required int OwnerPlayerIndex { get; init; }

	// Spend costs are paid once in GameEngine.PayActionCosts.
	public override GameState ApplyToState(GameState state)
	{
		return state;
	}

}
