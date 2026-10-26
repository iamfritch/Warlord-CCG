using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record DeclareAttackEvent : GameEvent
{
	public required int AttackerInstanceId { get; init; }

	public required int DefenderInstanceId { get; init; }

	public int[]? DefenderInstanceIds { get; init; }

	public int AdditionalWounds { get; set; }

	public override GameState ApplyToState(GameState state)
	{
		CardLocator.FindAndMutate(state, AttackerInstanceId, delegate(ref CardInstance card)
		{
			card.ReadyState = ReadyState.Spent;
		});
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private DeclareAttackEvent(DeclareAttackEvent original)
		: base(original)
	{
		AttackerInstanceId = original.AttackerInstanceId;
		DefenderInstanceId = original.DefenderInstanceId;
		DefenderInstanceIds = original.DefenderInstanceIds;
		AdditionalWounds = original.AdditionalWounds;
	}
}
