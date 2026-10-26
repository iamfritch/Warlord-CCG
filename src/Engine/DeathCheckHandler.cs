using System;
using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed class DeathCheckHandler : IPostResolutionHandler
{
	public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
	{
		int key;
		List<CardInstance> value;
		if (resolvedEvent is DealDamageEvent dealDamageEvent)
		{
			CardDatabase activeCardDb = GameEngine.GetActiveCardDb();
			foreach (PlayerState player in state.Players)
			{
				foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
				{
					rank.Deconstruct(out key, out value);
					List<CardInstance> list = value;
					for (int i = 0; i < list.Count; i++)
					{
						if (list[i].InstanceId == dealDamageEvent.TargetInstanceId)
						{
							CardInstance character = list[i];
							if (GameEngine.GetEffectiveCurrentHitPoints(state, character, activeCardDb) <= 0)
							{
								bool banishInsteadOfDiscard = dealDamageEvent.BanishOnDeath || (dealDamageEvent.SourceInstanceId.HasValue && IsBanishOnKillSource(state, dealDamageEvent.SourceInstanceId.Value)) || GameEngine.HasTurnBuff(state, dealDamageEvent.TargetInstanceId, "BANISH_ON_DEATH");
								yield return new CharacterDefeatedEvent
								{
									DefeatedInstanceId = dealDamageEvent.TargetInstanceId,
									OwnerPlayerIndex = player.PlayerIndex,
									BanishInsteadOfDiscard = banishInsteadOfDiscard,
									KillerInstanceId = dealDamageEvent.SourceInstanceId
								};
								yield break;
							}
						}
					}
				}
			}
		}
		else
		{
			if (!(resolvedEvent is CharacterDefeatedEvent))
			{
				yield break;
			}
			CardDatabase activeCardDb2 = GameEngine.GetActiveCardDb();
			foreach (PlayerState player2 in state.Players)
			{
				foreach (KeyValuePair<int, List<CardInstance>> rank2 in player2.Ranks)
				{
					rank2.Deconstruct(out key, out value);
					List<CardInstance> list2 = value;
					for (int j = 0; j < list2.Count; j++)
					{
						CardInstance character2 = list2[j];
						if (character2.Wounds > 0)
						{
							int effectiveMaxHitPoints = GameEngine.GetEffectiveMaxHitPoints(state, character2, activeCardDb2);
							int effectiveCurrentHitPoints = GameEngine.GetEffectiveCurrentHitPoints(state, character2, activeCardDb2);
							if (character2.Wounds >= effectiveMaxHitPoints || effectiveCurrentHitPoints <= 0)
							{
								yield return new CharacterDefeatedEvent
								{
									DefeatedInstanceId = character2.InstanceId,
									OwnerPlayerIndex = player2.PlayerIndex,
									BanishInsteadOfDiscard = false
								};
								yield break;
							}
						}
					}
				}
			}
		}
	}

	private static bool IsBanishOnKillSource(GameState state, int sourceId)
	{
		CardInstance? cardInstance = CardLocator.FindCard(state, sourceId);
		if (cardInstance.HasValue)
		{
			CardDefinition cardDefinition = GameEngine.GetActiveCardDb()?.GetDefinition(cardInstance.Value.DefinitionId);
			if (cardDefinition != null)
			{
				if (cardDefinition.Name.Equals("Shambling Horror", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
				if (cardDefinition.Name.Equals("Graveyard Librarian", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		return false;
	}
}
