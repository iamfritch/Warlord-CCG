using System.Collections.Generic;

namespace WarlordEngine.Engine;

public sealed record PassiveBonuses
{
	public int Atk { get; init; }

	public int Ac { get; init; }

	public int Hp { get; init; }

	public int Skill { get; init; }

	public int AdditionalWounds { get; init; }

	public bool TrueStrike { get; init; }

	public bool HasReach { get; init; }

	public List<StrikeDescriptor> AdditionalStrikes { get; init; } = new List<StrikeDescriptor>();
}
