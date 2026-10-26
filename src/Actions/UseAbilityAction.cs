using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record UseAbilityAction : PlayerAction
{
	public required int SourceInstanceId { get; init; }

	public required int AbilityIndex { get; init; }

	public int? ChoiceIndex { get; init; }

	public required int[] TargetInstanceIds { get; init; } = Array.Empty<int>();

	public int? DiscardCardInstanceId { get; init; }

	public int? CostCharacterInstanceId { get; init; }

	public int[] CostBanishCardInstanceIds { get; init; } = Array.Empty<int>();

	[CompilerGenerated]
	[SetsRequiredMembers]
	private UseAbilityAction(UseAbilityAction original)
		: base(original)
	{
		SourceInstanceId = original.SourceInstanceId;
		AbilityIndex = original.AbilityIndex;
		ChoiceIndex = original.ChoiceIndex;
		TargetInstanceIds = original.TargetInstanceIds;
		DiscardCardInstanceId = original.DiscardCardInstanceId;
		CostCharacterInstanceId = original.CostCharacterInstanceId;
		CostBanishCardInstanceIds = original.CostBanishCardInstanceIds;
	}
}
