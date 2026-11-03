namespace WarlordEngine.Actions;

public sealed record SelectStrikeTargetAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public int PlayerId
	{
		get => PlayerIndex;
		init => PlayerIndex = value;
	}

	public int? TargetInstanceId { get; init; }

	public SelectStrikeTargetAction()
	{
	}

	[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
	public SelectStrikeTargetAction(int playerIndex, int? targetInstanceId)
	{
		PlayerIndex = playerIndex;
		TargetInstanceId = targetInstanceId;
	}
}
