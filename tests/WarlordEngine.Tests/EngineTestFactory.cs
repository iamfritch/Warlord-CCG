using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WarlordEngine.Tests;

internal static class EngineTestFactory
{
	public static CardDatabase CreateCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["test-fighter"] = new CardDefinition
				{
					DefinitionId = "test-fighter",
					Name = "Test Fighter",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 1,
					ArmorClass = 15,
					Skill = 5,
					Attack = new AttackValue
					{
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 3,
							IsRanged = false
						}),
						RawText = "+3"
					},
					BaseHitPoints = 10
				},
				["test-action"] = new CardDefinition
				{
					DefinitionId = "test-action",
					Name = "Test Action Card",
					Type = CardType.Action,
					Level = 1
				}
			}
		};
	}

	public static GameState CreateMainPhaseState()
	{
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 1
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		return gameState;
	}

	public static GameState CreateCombatState(int attackerId = 1, int defenderId = 2, int attackerHp = 10, int defenderHp = 8)
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = attackerId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = attackerHp,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = defenderId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = defenderHp,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat,
			NextInstanceId = 100
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
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		return gameState;
	}
}
