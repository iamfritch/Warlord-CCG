using System;

namespace WarlordEngine.Actions;

public sealed record UseAbilityAction : IPlayerAction
{
	public required int PlayerIndex { get; init; }

	public required int SourceInstanceId { get; init; }

	public required int AbilityIndex { get; init; }

	public int? ChoiceIndex { get; init; }

	public required int[] TargetInstanceIds { get; init; } = Array.Empty<int>();

	public int? DiscardCardInstanceId { get; init; }

	public int? CostCharacterInstanceId { get; init; }

	public int[] CostBanishCardInstanceIds { get; init; } = Array.Empty<int>();
}
