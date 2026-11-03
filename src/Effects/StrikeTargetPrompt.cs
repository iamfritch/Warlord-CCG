using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WarlordEngine.Effects;

public sealed record StrikeTargetPrompt(
	[property: JsonIgnore] GameState State,
	CardInstance Attacker,
	int StrikeIndex,
	int TotalStrikes,
	StrikeDescriptor Strike,
	IReadOnlyList<CardInstance> LegalTargets,
	int? PreviousTargetId);
