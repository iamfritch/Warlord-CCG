using System.Collections.Generic;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed class WarlordEliminationHandler : IPostResolutionHandler
{
	public IEnumerable<GameEvent> AfterResolve(GameEvent resolvedEvent, GameState state)
	{
		if (state.IsGameOver)
		{
			yield break;
		}
		if (resolvedEvent is CharacterDefeatedEvent characterDefeatedEvent)
		{
			PlayerState playerState = state.Players[characterDefeatedEvent.OwnerPlayerIndex];
			if (!playerState.WarlordInstanceId.HasValue || playerState.WarlordInstanceId == characterDefeatedEvent.DefeatedInstanceId)
			{
				int value = 1 - characterDefeatedEvent.OwnerPlayerIndex;
				yield return new GameOverEvent
				{
					WinningPlayerIndex = value,
					Reason = GameOverReason.WarlordDefeated,
					DefeatedPlayerIndex = characterDefeatedEvent.OwnerPlayerIndex
				};
			}
		}
		else
		{
			if (!(resolvedEvent is DealDamageEvent dealDamageEvent))
			{
				yield break;
			}
			foreach (PlayerState player in state.Players)
			{
				if (player.WarlordInstanceId != dealDamageEvent.TargetInstanceId)
				{
					continue;
				}
				foreach (List<CardInstance> value3 in player.Ranks.Values)
				{
					foreach (CardInstance item in value3)
					{
						if (item.InstanceId == dealDamageEvent.TargetInstanceId && item.CurrentHitPoints <= 0)
						{
							int value2 = 1 - player.PlayerIndex;
							yield return new GameOverEvent
							{
								WinningPlayerIndex = value2,
								Reason = GameOverReason.WarlordDefeated,
								DefeatedPlayerIndex = player.PlayerIndex
							};
							yield break;
						}
					}
				}
			}
		}
	}
}
