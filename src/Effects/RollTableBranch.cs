using System.Collections.Generic;

namespace WarlordEngine.Effects;

public sealed record RollTableBranch(int MinRoll, int MaxRoll, IReadOnlyList<IEffect> Effects);
