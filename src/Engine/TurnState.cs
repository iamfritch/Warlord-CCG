using System;
using System.Collections.Generic;

namespace WarlordEngine.Engine;

public sealed class TurnState
{
	public int ActivePlayerIndex { get; set; }

	public TurnPhase CurrentPhase { get; set; }

	public int TurnNumber { get; set; } = 1;

	public PriorityState Priority { get; set; } = new PriorityState();

	public HashSet<string> UsedLimitedAbilityKeysThisTurn { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public void RecordPass()
	{
		Priority.ConsecutivePasses++;
		Priority.HasPassed = true;
		if (CurrentPhase == TurnPhase.Main)
		{
			ActivePlayerIndex = 1 - ActivePlayerIndex;
			Priority.ActivePlayerIndex = ActivePlayerIndex;
		}
	}

	public void RecordAction()
	{
		Priority.ConsecutivePasses = 0;
		Priority.HasPassed = false;
		if (CurrentPhase == TurnPhase.Main)
		{
			ActivePlayerIndex = 1 - ActivePlayerIndex;
			Priority.ActivePlayerIndex = ActivePlayerIndex;
		}
	}

	public TurnState DeepClone()
	{
		return new TurnState
		{
			ActivePlayerIndex = ActivePlayerIndex,
			CurrentPhase = CurrentPhase,
			TurnNumber = TurnNumber,
			Priority = Priority.DeepClone(),
			UsedLimitedAbilityKeysThisTurn = new HashSet<string>(UsedLimitedAbilityKeysThisTurn, StringComparer.OrdinalIgnoreCase)
		};
	}
}
