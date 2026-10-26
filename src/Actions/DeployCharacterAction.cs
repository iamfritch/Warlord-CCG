using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record DeployCharacterAction : PlayerAction
{
	public required string DefinitionId { get; init; }

	public required int TargetRank { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DeployCharacterAction(DeployCharacterAction original)
		: base(original)
	{
		DefinitionId = original.DefinitionId;
		TargetRank = original.TargetRank;
	}
}
