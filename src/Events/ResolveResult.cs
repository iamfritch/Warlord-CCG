using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record ResolveResult
{
	public required GameEvent OriginalEvent { get; init; }

	public required GameEvent FinalEvent { get; init; }

	public required ResolutionStatus Status { get; init; }

	public required bool WasApplied { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ResolveResult(ResolveResult original)
	{
		OriginalEvent = original.OriginalEvent;
		FinalEvent = original.FinalEvent;
		Status = original.Status;
		WasApplied = original.WasApplied;
	}
}
