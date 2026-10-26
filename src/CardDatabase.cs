using System.Collections.Generic;

namespace WarlordEngine;

public sealed class CardDatabase
{
	public IReadOnlyDictionary<string, CardDefinition> Definitions { get; init; } = new Dictionary<string, CardDefinition>();

	public CardDefinition? GetDefinition(string definitionId)
	{
		if (!Definitions.TryGetValue(definitionId, out CardDefinition value))
		{
			return null;
		}
		return value;
	}
}
