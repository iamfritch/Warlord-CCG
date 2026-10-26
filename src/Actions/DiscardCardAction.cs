namespace WarlordEngine.Actions;

public sealed record DiscardCardAction : PlayerAction
{
	public required int CardInstanceId { get; init; }
}
