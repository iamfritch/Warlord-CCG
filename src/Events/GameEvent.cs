using System;

namespace WarlordEngine.Events;

public abstract record GameEvent
{
	public Guid EventId { get; init; } = Guid.NewGuid();

	public abstract GameState ApplyToState(GameState state);
}
