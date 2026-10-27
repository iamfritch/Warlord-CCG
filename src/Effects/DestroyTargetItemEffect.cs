using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Engine;
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
		if (!cardInstance.HasValue)
		{
			foreach (PlayerState player in context.State.Players)
			{
				foreach (List<CardInstance> rank in player.Ranks.Values)
				{
					foreach (CardInstance candidate in rank)
					{
						if (candidate.AttachedItemIds.Contains(targetItemId))
						{
							cardInstance = candidate;
							break;
						}
					}
					if (cardInstance.HasValue)
					{
						break;
					}
				}
				if (cardInstance.HasValue)
				{
					break;
				}
			}
		}
		CardInstance? cardInstance3 = context.FindCard(targetItemId);
		if (!cardInstance3.HasValue)
		{
			yield break;
		}
		CardInstance? performer = context.GetPerformerCharacter();
		if (performer.HasValue && cardInstance.HasValue && performer.Value.Rank.HasValue && cardInstance.Value.Rank.HasValue)
		{
			int dist = performer.Value.OwnerPlayerIndex == cardInstance.Value.OwnerPlayerIndex
				? Math.Abs(performer.Value.Rank.Value - cardInstance.Value.Rank.Value)
				: BoardGeometry.GetCrossBoardDistance(performer.Value.Rank.Value, cardInstance.Value.Rank.Value);
			if (dist > MaxRankDistance)
			{
				yield break;
			}
		}
		CardDefinition? itemDef = context.CardDb.GetDefinition(cardInstance3.Value.DefinitionId);
		if (performer.HasValue && itemDef != null && itemDef.Level >= GameEngine.GetEffectiveLevel(context.State, performer.Value, context.CardDb) + LevelOffset)
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
		foreach (PlayerState p in context.State.Players)
		{
			p.AttachedItems.RemoveAll((CardInstance c) => c.InstanceId == targetItemId);
		}
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
