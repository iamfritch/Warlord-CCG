using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WarlordEngine.Tests;

internal static class TestStateFactory
{
	public static (GameState State, CardInstance Card) CreateWithOneCharacterOnBoard(int hp = 10, int ac = 15, int instanceId = 1)
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = instanceId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = hp,
			CurrentArmorClass = ac,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat,
			NextInstanceId = instanceId + 1
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		GameState item = gameState;
		return (State: item, Card: cardInstance);
	}
}
