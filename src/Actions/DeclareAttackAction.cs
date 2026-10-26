using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record DeclareAttackAction : PlayerAction
{
	public required int AttackerInstanceId { get; init; }

	public required int DefenderInstanceId { get; init; }

	public int[]? TargetInstanceIds { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DeclareAttackAction(DeclareAttackAction original)
		: base(original)
	{
		AttackerInstanceId = original.AttackerInstanceId;
		DefenderInstanceId = original.DefenderInstanceId;
		TargetInstanceIds = original.TargetInstanceIds;
	}
}
