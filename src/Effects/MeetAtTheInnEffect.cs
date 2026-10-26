using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record MeetAtTheInnEffect : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		int casterPlayer = context.FindCard(context.SourceInstanceId)?.OwnerPlayerIndex ?? context.State.ActivePlayerIndex;
		yield return new DrawCardEvent
		{
			PlayerIndex = casterPlayer
		};
		yield return new DrawCardEvent
		{
			PlayerIndex = casterPlayer
		};
		for (int p = 0; p < context.State.Players.Count; p++)
		{
			if (p != casterPlayer)
			{
				yield return new DrawCardEvent
				{
					PlayerIndex = p
				};
			}
		}
	}
}
