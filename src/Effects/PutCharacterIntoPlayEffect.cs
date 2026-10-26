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
			playerState.Hand.RemoveAll((CardInstance c) => c.InstanceId == charInHand.InstanceId);
			int num = context.CardDb.GetDefinition(charInHand.DefinitionId)?.Level ?? 1;
			int num2 = (ReduceRank ? Math.Max(1, num - 1) : Math.Max(1, num));
			if (!playerState.Ranks.TryGetValue(num2, out List<CardInstance> value))
			{
				value = new List<CardInstance>();
				playerState.Ranks[num2] = value;
			}
			value.Add(charInHand with
			{
				Zone = CardZone.Board,
				Rank = num2
			});
		}
	}
}
