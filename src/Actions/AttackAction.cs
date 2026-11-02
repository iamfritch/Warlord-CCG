namespace WarlordEngine.Actions;

public sealed record AttackAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public required int AttackerInstanceId { get; init; }

	public required int TargetInstanceId { get; init; }

	public int[]? TargetInstanceIds { get; init; }
}
