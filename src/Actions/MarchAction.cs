using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public record MarchAction : PlayerAction
{
	public required int CardInstanceId { get; init; }

	public required int SourceRank { get; init; }

	public required int DestinationRank { get; init; }

	public int? TargetSlotIndex { get; init; }

	public MarchAction()
	{
	}

	[SetsRequiredMembers]
	protected MarchAction(MarchAction original)
		: base(original)
	{
		CardInstanceId = original.CardInstanceId;
		SourceRank = original.SourceRank;
		DestinationRank = original.DestinationRank;
		TargetSlotIndex = original.TargetSlotIndex;
	}
}
