using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Effects;

public sealed record FireballEffect(int StrikeBonus = 4, int SaveDC = 9) : IEffect
{
	public IEnumerable<GameEvent> Evaluate(EffectContext context)
	{
		if (!context.TargetInstanceId.HasValue)
		{
			yield break;
		}
		int value = context.TargetInstanceId.Value;
		CardInstance? cardInstance = context.FindCard(value);
		if (!cardInstance.HasValue)
		{
			yield break;
		}
		int ownerPlayerIndex = cardInstance.Value.OwnerPlayerIndex;
		List<CardInstance> adjacentBeforeStrike = ((ownerPlayerIndex >= 0 && ownerPlayerIndex < context.State.Players.Count) ? BoardGeometry.GetAdjacentCharacters(context.State.Players[ownerPlayerIndex], value) : new List<CardInstance>());
		PerformStrikeEffect performStrikeEffect = new PerformStrikeEffect(StrikeBonus, IsRanged: true);
		foreach (GameEvent item in performStrikeEffect.Evaluate(context))
		{
			yield return item;
		}
		foreach (CardInstance item2 in adjacentBeforeStrike)
		{
			CardInstance? cardInstance2 = context.FindCard(item2.InstanceId);
			if (!cardInstance2.HasValue || cardInstance2.Value.Zone != CardZone.Board)
			{
				continue;
			}
			int num = 0;
			if (cardInstance2.Value.SkillRatings.TryGetValue("Magic Resistance", out var value2))
			{
				num = value2;
			}
			else if (context.CardDb != null)
			{
				CardDefinition definition = context.CardDb.GetDefinition(cardInstance2.Value.DefinitionId);
				if (definition != null)
				{
					foreach (string trait in definition.Traits)
					{
						if (trait.StartsWith("Magic Resistance", StringComparison.OrdinalIgnoreCase))
						{
							string[] array = trait.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
							if (array.Length > 1 && int.TryParse(array[1], out var result))
							{
								num = result;
								break;
							}
						}
					}
				}
			}
			int num2 = context.RollDice(1, 20);
			if (num2 != 20 && (num2 == 1 || num2 + num < SaveDC))
			{
				yield return new DealDamageEvent
				{
					TargetInstanceId = cardInstance2.Value.InstanceId,
					Amount = 1,
					DamageType = "magic",
					SourceInstanceId = context.SourceInstanceId
				};
			}
		}
	}
}
