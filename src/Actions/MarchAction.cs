using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Actions;

public sealed record MarchAction : PlayerAction
{
	public required int CardInstanceId { get; init; }

	public required int SourceRank { get; init; }

	public required int DestinationRank { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private MarchAction(MarchAction original)
		: base(original)
	{
		CardInstanceId = original.CardInstanceId;
		SourceRank = original.SourceRank;
		DestinationRank = original.DestinationRank;
	}
}
