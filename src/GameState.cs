using System;
using System.Collections.Generic;
using WarlordEngine.Engine;
using WarlordEngine.Events;

namespace WarlordEngine;

public sealed class GameState
{
	private bool _isSimulation;

	public List<PlayerState> Players { get; set; } = new List<PlayerState>();

	public TurnState TurnState { get; set; } = new TurnState();

	public bool AutoPassWhenNoReactsAvailable { get; set; } = true;

	public List<GameEvent> PendingEvents { get; set; } = new List<GameEvent>();

	public int TurnNumber
	{
		get
		{
			return TurnState.TurnNumber;
		}
		set
		{
			TurnState.TurnNumber = value;
		}
	}

	public int ActivePlayerIndex
	{
		get
		{
			return TurnState.ActivePlayerIndex;
		}
		set
		{
			TurnState.ActivePlayerIndex = value;
		}
	}

	public TurnPhase CurrentPhase
	{
		get
		{
			return TurnState.CurrentPhase;
		}
		set
		{
			TurnState.CurrentPhase = value;
		}
	}

	public PriorityState Priority
	{
		get
		{
			return TurnState.Priority;
		}
		set
		{
			TurnState.Priority = value;
		}
	}

	public bool IsGameOver { get; set; }

	public int? WinningPlayerIndex { get; set; }

	public GameOverReason GameOverReason { get; set; }

	public int NextInstanceId { get; set; } = 1;

	public bool IsSimulation
	{
		get
		{
			return _isSimulation;
		}
		set
		{
			_isSimulation = value;
			for (int i = 0; i < Players.Count; i++)
			{
				Players[i].IsSimulation = value;
			}
		}
	}

	public GameState DeepClone(int? observerPlayerId = null)
	{
		bool isSimulation = _isSimulation || observerPlayerId.HasValue;
		GameState gameState = new GameState
		{
			TurnState = TurnState.DeepClone(),
			AutoPassWhenNoReactsAvailable = AutoPassWhenNoReactsAvailable,
			IsGameOver = IsGameOver,
			WinningPlayerIndex = WinningPlayerIndex,
			GameOverReason = GameOverReason,
			NextInstanceId = NextInstanceId,
			Players = new List<PlayerState>(Players.Count),
			PendingEvents = new List<GameEvent>(PendingEvents.Count),
			_isSimulation = isSimulation
		};
		for (int i = 0; i < PendingEvents.Count; i++)
		{
			GameEvent gameEvent = PendingEvents[i];
			if (gameEvent is AbilityActivatedEvent abilityActivatedEvent)
			{
				gameState.PendingEvents.Add(abilityActivatedEvent with
				{
					TargetInstanceIds = (int[])abilityActivatedEvent.TargetInstanceIds.Clone()
				});
			}
			else
			{
				gameState.PendingEvents.Add(gameEvent);
			}
		}
		for (int j = 0; j < Players.Count; j++)
		{
			PlayerState playerState = Players[j].DeepClone();
			playerState.IsSimulation = isSimulation;
			if (observerPlayerId.HasValue && playerState.PlayerIndex != observerPlayerId.Value)
			{
				DeterminizeHiddenZones(playerState);
			}
			gameState.Players.Add(playerState);
		}
		return gameState;
	}

	private static void DeterminizeHiddenZones(PlayerState opponent)
	{
		int count = opponent.Hand.Count;
		int count2 = opponent.Deck.Count;
		int num = count + count2;
		if (num <= 1)
		{
			return;
		}
		int[] array = new int[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = opponent.Hand[i].InstanceId;
		}
		List<CardInstance> list = new List<CardInstance>(num);
		list.AddRange(opponent.Hand);
		list.AddRange(opponent.Deck);
		for (int num2 = list.Count - 1; num2 > 0; num2--)
		{
			int num3 = Random.Shared.Next(num2 + 1);
			List<CardInstance> list2 = list;
			int index = num2;
			int index2 = num3;
			CardInstance value = list[num3];
			CardInstance value2 = list[num2];
			list2[index] = value;
			list[index2] = value2;
		}
		if (count > 0 && IsSameHandSequence(list, array))
		{
			if (count2 > 0)
			{
				int num4 = Random.Shared.Next(count);
				int num5 = count + Random.Shared.Next(count2);
				List<CardInstance> list2 = list;
				int index2 = num4;
				int index = num5;
				CardInstance value2 = list[num5];
				CardInstance value = list[num4];
				list2[index2] = value2;
				list[index] = value;
			}
			else if (count > 1)
			{
				List<CardInstance> list2 = list;
				int index = count - 1;
				CardInstance value = list[count - 1];
				CardInstance value2 = list[0];
				list[0] = value;
				list2[index] = value2;
			}
		}
		opponent.Hand.Clear();
		opponent.Deck.Clear();
		for (int j = 0; j < count; j++)
		{
			opponent.Hand.Add(list[j]with
			{
				Zone = CardZone.Hand
			});
		}
		for (int k = count; k < num; k++)
		{
			opponent.Deck.Add(list[k]with
			{
				Zone = CardZone.Deck
			});
		}
	}

	private static bool IsSameHandSequence(List<CardInstance> pool, int[] originalHandIds)
	{
		for (int i = 0; i < originalHandIds.Length; i++)
		{
			if (pool[i].InstanceId != originalHandIds[i])
			{
				return false;
			}
		}
		return true;
	}
}
