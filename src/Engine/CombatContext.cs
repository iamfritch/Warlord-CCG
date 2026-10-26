namespace WarlordEngine.Engine;

public sealed record CombatContext
{
	public bool IsAttacking { get; init; }

	public bool IsDefending { get; init; }

	public CardInstance? OpponentCard { get; init; }

	public bool IsMelee { get; init; } = true;

	public bool IsRanged { get; init; }
}
