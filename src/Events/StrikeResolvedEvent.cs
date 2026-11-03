using WarlordEngine.Effects;

namespace WarlordEngine.Events;

public sealed record StrikeResolvedEvent : GameEvent
{
	public required AttackResolutionDetails Details { get; init; }

	public StrikeResolvedEvent()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public StrikeResolvedEvent(AttackResolutionDetails details)
	{
		Details = details;
	}

	public override GameState ApplyToState(GameState state)
	{
		return state;
	}
}
