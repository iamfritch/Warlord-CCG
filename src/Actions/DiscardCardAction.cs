namespace WarlordEngine.Actions;

public sealed record DiscardCardAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public int PlayerId
	{
		get => PlayerIndex;
		init => PlayerIndex = value;
	}

	public required int CardInstanceId { get; init; }

	public DiscardCardAction()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public DiscardCardAction(int playerIndex, int cardInstanceId)
	{
		PlayerIndex = playerIndex;
		CardInstanceId = cardInstanceId;
	}
}
