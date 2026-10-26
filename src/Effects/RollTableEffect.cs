using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record RollTableEffect(IReadOnlyList<RollTableBranch> Branches) : IEffect
{
	public static Action<int, int, int, int>? GlobalOnRollTableResolved { get; set; }

	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int num = context.RollDice(1, 20);
		foreach (RollTableBranch branch in Branches)
		{
			if (num < branch.MinRoll || num > branch.MaxRoll)
			{
				continue;
			}
			GlobalOnRollTableResolved?.Invoke(context.SourceInstanceId, num, branch.MinRoll, branch.MaxRoll);
			foreach (IEffect effect in branch.Effects)
			{
				foreach (GameEvent item in effect.Evaluate(context))
				{
					yield return item;
				}
			}
			break;
		}
	}
}
