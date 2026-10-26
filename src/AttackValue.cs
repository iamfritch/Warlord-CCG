using System;
using System.Collections.Generic;

namespace WarlordEngine;

public sealed record AttackValue
{
	public IReadOnlyList<StrikeDescriptor> Strikes { get; init; } = Array.Empty<StrikeDescriptor>();

	public string RawText { get; init; } = string.Empty;
}
