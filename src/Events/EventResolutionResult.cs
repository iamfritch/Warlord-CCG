using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record EventResolutionResult
{
	public required ResolutionStatus Status { get; init; }

	public GameEvent? ModifiedEvent { get; init; }

	public static EventResolutionResult Allow()
	{
		return new EventResolutionResult
		{
			Status = ResolutionStatus.Allowed
		};
	}

	public static EventResolutionResult Cancel()
	{
		return new EventResolutionResult
		{
			Status = ResolutionStatus.Cancelled
		};
	}

	public static EventResolutionResult Modify(GameEvent modifiedEvent)
	{
		return new EventResolutionResult
		{
			Status = ResolutionStatus.Modified,
			ModifiedEvent = modifiedEvent
		};
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private EventResolutionResult(EventResolutionResult original)
	{
		Status = original.Status;
		ModifiedEvent = original.ModifiedEvent;
	}
}
