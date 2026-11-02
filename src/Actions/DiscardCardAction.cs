namespace WarlordEngine.Actions;

public sealed record DiscardCardAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }
}
