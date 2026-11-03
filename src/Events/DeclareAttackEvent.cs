namespace WarlordEngine.Events;

public sealed record DeclareAttackEvent : GameEvent
{
	public required int AttackerInstanceId { get; init; }

	public required int DefenderInstanceId { get; init; }

	public int[]? DefenderInstanceIds { get; init; }

	public int AdditionalWounds { get; set; }

	public int StartStrikeIndex { get; init; }

	public bool SkipRemainingStrikes { get; init; }

	// Attack costs (spending the attacker) are paid once in GameEngine.PayActionCosts.
	public override GameState ApplyToState(GameState state)
	{
		return state;
	}

}
