using System.Collections.Generic;

namespace WarlordEngine.Effects;

public sealed record StrikeTargetPrompt(GameState State, CardInstance Attacker, int StrikeIndex, int TotalStrikes, StrikeDescriptor Strike, IReadOnlyList<CardInstance> LegalTargets, int? PreviousTargetId);
