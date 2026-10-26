using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record AttackAction : PlayerAction, IPlayerAction
{
	public required int AttackerInstanceId { get; init; }

	public required int TargetInstanceId { get; init; }

	public int[]? TargetInstanceIds { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private AttackAction(AttackAction original)
		: base(original)
	{
		AttackerInstanceId = original.AttackerInstanceId;
		TargetInstanceId = original.TargetInstanceId;
		TargetInstanceIds = original.TargetInstanceIds;
	}
}
