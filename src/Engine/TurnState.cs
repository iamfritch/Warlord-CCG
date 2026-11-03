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

	public Dictionary<int, List<TurnBuff>> ActiveTurnBuffs { get; init; } = new();

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

	public void ResetForNewTurn()
	{
		ActiveTurnBuffs.Clear();
		UsedLimitedAbilityKeysThisTurn.Clear();
	}

	public TurnState Clone() => DeepClone();

	public TurnState DeepClone()
	{
		var clonedBuffs = new Dictionary<int, List<TurnBuff>>(ActiveTurnBuffs.Count);
		foreach (var (key, list) in ActiveTurnBuffs)
		{
			clonedBuffs[key] = new List<TurnBuff>(list);
		}

		return new TurnState
		{
			ActivePlayerIndex = ActivePlayerIndex,
			CurrentPhase = CurrentPhase,
			TurnNumber = TurnNumber,
			Priority = Priority.DeepClone(),
			UsedLimitedAbilityKeysThisTurn = new HashSet<string>(UsedLimitedAbilityKeysThisTurn, StringComparer.OrdinalIgnoreCase),
			ActiveTurnBuffs = clonedBuffs
		};
	}
}
