namespace WarlordEngine;

public sealed record StrikeDescriptor
{
	public int Modifier { get; init; }

	public bool IsRanged { get; init; }
}
