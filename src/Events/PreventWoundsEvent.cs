using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record PreventWoundsEvent : GameEvent
{
	public required int TargetInstanceId { get; init; }

	public required int Amount { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		CardLocator.FindAndMutate(state, TargetInstanceId, delegate(ref CardInstance card)
		{
			card.StatusTags.Add($"prevent_wounds:{Amount}");
		});
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PreventWoundsEvent(PreventWoundsEvent original)
		: base(original)
	{
		TargetInstanceId = original.TargetInstanceId;
		Amount = original.Amount;
	}
}
