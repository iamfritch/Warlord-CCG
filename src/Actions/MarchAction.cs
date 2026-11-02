namespace WarlordEngine.Actions;

public record MarchAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }

	public required int SourceRank { get; init; }

	public required int DestinationRank { get; init; }

	public int? TargetSlotIndex { get; init; }
}
