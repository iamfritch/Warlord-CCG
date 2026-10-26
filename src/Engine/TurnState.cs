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

	public void AdvancePhase()
	{
		switch (CurrentPhase)
		{
		case TurnPhase.Setup:
			CurrentPhase = TurnPhase.Ready;
			Priority.ConsecutivePasses = 0;
			UsedLimitedAbilityKeysThisTurn.Clear();
			break;
		case TurnPhase.Ready:
			CurrentPhase = TurnPhase.Draw;
			break;
		case TurnPhase.Draw:
			CurrentPhase = TurnPhase.Main;
			Priority.ConsecutivePasses = 0;
			break;
		case TurnPhase.Main:
			CurrentPhase = TurnPhase.Combat;
			break;
		case TurnPhase.Combat:
			CurrentPhase = TurnPhase.End;
			break;
		case TurnPhase.End:
			CurrentPhase = TurnPhase.Ready;
			ActivePlayerIndex = 1 - ActivePlayerIndex;
			TurnNumber++;
			Priority.ConsecutivePasses = 0;
			UsedLimitedAbilityKeysThisTurn.Clear();
			break;
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
