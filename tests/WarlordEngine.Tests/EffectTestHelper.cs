using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WarlordEngine.Effects;

namespace WarlordEngine.Tests;

internal static class EffectTestHelper
{
	public static readonly CardDatabase TestCardDb = new CardDatabase
	{
		Definitions = new Dictionary<string, CardDefinition> { ["test-fighter"] = new CardDefinition
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
		} }
	};

	public static EffectContext CreateCombatContext(int fixedRoll, int attackerInstanceId = 1, int defenderInstanceId = 2, int defenderAC = 15)
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = attackerInstanceId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = defenderInstanceId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = defenderAC,
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
		GameState state = gameState;
		return new EffectContext
		{
			SourceInstanceId = attackerInstanceId,
			TargetInstanceId = defenderInstanceId,
			State = state,
			CardDb = TestCardDb,
			RollDice = (int _, int _) => fixedRoll
		};
	}

	public static EffectContext CreateBoardContext(int sourceInstanceId = 1, int? targetInstanceId = null, int sourceRank = 1, int sourcePlayerIndex = 0)
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = sourceInstanceId,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = sourcePlayerIndex
		};
		CardInstance? cardInstance2 = (targetInstanceId.HasValue ? new CardInstance?(new CardInstance
		{
			InstanceId = targetInstanceId.Value,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		}) : ((CardInstance?)null));
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		int num = 2;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list);
		span[0] = new CardInstance
		{
			InstanceId = 50,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 0
		};
		span[1] = new CardInstance
		{
			InstanceId = 51,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			Zone = CardZone.Deck,
			OwnerPlayerIndex = 0
		};
		playerState.Deck = list;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num = sourceRank;
		int key = num;
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[key] = list2;
		playerState.Ranks = sortedDictionary;
		PlayerState playerState2 = playerState;
		PlayerState playerState3 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		if (cardInstance2.HasValue)
		{
			SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
			num = 1;
			List<CardInstance> list3 = new List<CardInstance>(num);
			CollectionsMarshal.SetCount(list3, num);
			CollectionsMarshal.AsSpan(list3)[0] = cardInstance2.Value;
			sortedDictionary2[1] = list3;
			playerState3.Ranks = sortedDictionary2;
		}
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 100
		};
		num = 2;
		List<PlayerState> list4 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<PlayerState> span2 = CollectionsMarshal.AsSpan(list4);
		span2[0] = playerState2;
		span2[1] = playerState3;
		gameState.Players = list4;
		GameState state = gameState;
		return new EffectContext
		{
			SourceInstanceId = sourceInstanceId,
			TargetInstanceId = targetInstanceId,
			State = state,
			CardDb = TestCardDb
		};
	}
}
