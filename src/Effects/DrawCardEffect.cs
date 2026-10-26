using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record DrawCardEffect(int Amount) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (cardInstance.HasValue)
		{
			int playerIndex = cardInstance.Value.OwnerPlayerIndex;
			for (int i = 0; i < Amount; i++)
			{
				yield return new DrawCardEvent
				{
					PlayerIndex = playerIndex
				};
			}
		}
	}
}
