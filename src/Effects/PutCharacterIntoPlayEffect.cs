using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record PutCharacterIntoPlayEffect(bool ReduceRank = true) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		CardInstance? cardInstance = context.FindCard(context.SourceInstanceId);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		PlayerState playerState = context.State.Players[cardInstance.Value.OwnerPlayerIndex];
		CardInstance charInHand = playerState.Hand.FirstOrDefault((CardInstance c) =>
		{
			CardDefinition definition = context.CardDb.GetDefinition(c.DefinitionId);
			return definition != null && definition.Type == CardType.Character;
		});
		if (charInHand.InstanceId != 0)
		{
			int level = context.CardDb.GetDefinition(charInHand.DefinitionId)?.Level ?? 1;
			int targetRank = (ReduceRank ? Math.Max(1, level - 1) : Math.Max(1, level));
			yield return new PlayCardEvent
			{
				PlayerIndex = cardInstance.Value.OwnerPlayerIndex,
				CardInstanceId = charInHand.InstanceId,
				TargetRank = targetRank,
				SkipEntryChecks = true
			};
		}
	}
}
