using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record ChangeReadyStateEvent : GameEvent
{
	public required int TargetInstanceId { get; init; }

	public required ReadyState NewReadyState { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		CardLocator.FindAndMutate(state, TargetInstanceId, delegate(ref CardInstance card)
		{
			card.ReadyState = NewReadyState;
		});
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ChangeReadyStateEvent(ChangeReadyStateEvent original)
		: base(original)
	{
		TargetInstanceId = original.TargetInstanceId;
		NewReadyState = original.NewReadyState;
	}
}
