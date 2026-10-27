using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record FortitudeSaveEffect(int DC, IReadOnlyList<IEffect> FailureEffects, bool PerEquippedItem = false, bool AddCasterSkill = false) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (PerEquippedItem && !context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int instanceId = context.TargetInstanceId ?? context.SourceInstanceId;
		CardInstance? cardInstance = context.FindCard(instanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		if (PerEquippedItem)
		{
			CardInstance? performer = context.GetPerformerCharacter();
			if (performer.HasValue && performer.Value.Rank.HasValue && cardInstance.Value.Rank.HasValue)
			{
				int dist = performer.Value.OwnerPlayerIndex == cardInstance.Value.OwnerPlayerIndex
					? Math.Abs(performer.Value.Rank.Value - cardInstance.Value.Rank.Value)
					: BoardGeometry.GetCrossBoardDistance(performer.Value.Rank.Value, cardInstance.Value.Rank.Value);
				if (dist > 2)
				{
					yield break;
				}
			}
		}
		int effectiveDC = DC;
		if (AddCasterSkill)
		{
			CardInstance? cardInstance2 = context.FindCard(context.SourceInstanceId);
			if (cardInstance2.HasValue)
			{
				effectiveDC += GameEngine.GetEffectiveSkill(context.State, cardInstance2.Value, context.CardDb);
			}
		}
		int targetSkill = GameEngine.GetEffectiveSkill(context.State, cardInstance.Value, context.CardDb);
		int attachedCount = cardInstance.Value.AttachedItemIds.Count;
		if (attachedCount == 0 && cardInstance.Value.OwnerPlayerIndex >= 0 && cardInstance.Value.OwnerPlayerIndex < context.State.Players.Count)
		{
			int targetId = cardInstance.Value.InstanceId;
			attachedCount = context.State.Players[cardInstance.Value.OwnerPlayerIndex].AttachedItems.Count((CardInstance c) => c.AttachedToInstanceId == targetId);
		}
		int checks = ((!PerEquippedItem) ? 1 : attachedCount);
		for (int i = 0; i < checks; i++)
		{
			int roll = context.RollDice(1, 20);
			bool success = roll == 20 || (roll != 1 && roll + targetSkill >= effectiveDC);
			if (success)
			{
				continue;
			}
			foreach (IEffect failureEffect in FailureEffects)
			{
				foreach (GameEvent item in failureEffect.Evaluate(context))
				{
					yield return item;
				}
			}
		}
	}
}
