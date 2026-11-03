namespace WarlordEngine.Events;

public sealed record RollTableResolvedEvent : GameEvent
{
	public required int SourceInstanceId { get; init; }

	public required int Roll { get; init; }

	public required int MinRoll { get; init; }

	public required int MaxRoll { get; init; }

	public RollTableResolvedEvent()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public RollTableResolvedEvent(int sourceInstanceId, int roll, int minRoll, int maxRoll)
	{
		SourceInstanceId = sourceInstanceId;
		Roll = roll;
		MinRoll = minRoll;
		MaxRoll = maxRoll;
	}

	public override GameState ApplyToState(GameState state)
	{
		return state;
	}
}
