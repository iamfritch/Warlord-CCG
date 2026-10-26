using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record DestroyTargetItemEffect(int MaxRankDistance = 2, int LevelOffset = 5) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int value = context.TargetInstanceId.Value;
		int targetItemId = -1;
		CardInstance? cardInstance = null;
		CardInstance? cardInstance2 = context.FindCard(value);
		if (cardInstance2.HasValue)
		{
			if (cardInstance2.Value.Zone == CardZone.Attached)
			{
				targetItemId = cardInstance2.Value.InstanceId;
				if (cardInstance2.Value.AttachedToInstanceId.HasValue)
				{
					cardInstance = context.FindCard(cardInstance2.Value.AttachedToInstanceId.Value);
				}
			}
			else if (cardInstance2.Value.AttachedItemIds.Count > 0)
			{
				cardInstance = cardInstance2;
				targetItemId = cardInstance2.Value.AttachedItemIds[0];
			}
		}
		if (targetItemId < 0)
		{
			yield break;
		}
		CardInstance? cardInstance3 = context.FindCard(targetItemId);
		if (!cardInstance3.HasValue)
		{
			yield break;
		}
		if (cardInstance.HasValue)
		{
			CardLocator.FindAndMutate(context.State, cardInstance.Value.InstanceId, delegate(ref CardInstance c)
			{
				c.AttachedItemIds.Remove(targetItemId);
			});
		}
		CardLocator.FindAndMutate(context.State, targetItemId, delegate(ref CardInstance c)
		{
			c.Zone = CardZone.Discard;
			c.AttachedToInstanceId = null;
		});
		PlayerState playerState = context.State.Players[cardInstance3.Value.OwnerPlayerIndex];
		if (!playerState.DiscardPile.Any((CardInstance c) => c.InstanceId == targetItemId))
		{
			playerState.DiscardPile.Add(cardInstance3.Value with
			{
				Zone = CardZone.Discard,
				AttachedToInstanceId = null
			});
		}
	}
}
