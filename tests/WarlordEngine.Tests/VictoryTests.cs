using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class VictoryTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["warlord-hero"] = new CardDefinition
			{
				DefinitionId = "warlord-hero",
				Name = "General Warlord",
				Type = CardType.Character,
				Class = CharacterClass.Fighter,
				Level = 5,
				ArmorClass = 15,
				BaseHitPoints = 10,
				IsWarlord = true,
				Attack = new AttackValue
				{
					Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
					{
						Modifier = 5,
						IsRanged = false
					} },
					RawText = "+5"
				}
			} }
		};
	}

	[Fact]
	public void Defeating_Warlord_Triggers_GameOver_And_Assigns_Opponent_As_Winner()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 1,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1",
			WarlordInstanceId = 1
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
			DisplayName = "Player 2",
			WarlordInstanceId = 2
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
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.True(gameState2.IsGameOver);
		Assert.Equal(0, gameState2.WinningPlayerIndex);
		Assert.Equal(GameOverReason.WarlordDefeated, gameState2.GameOverReason);
		Assert.Null(gameState2.Players[1].WarlordInstanceId);
	}

	[Fact]
	public void Further_Actions_Halted_When_Game_Is_Over()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState gameState = new GameState
		{
			IsGameOver = true,
			WinningPlayerIndex = 0,
			GameOverReason = GameOverReason.WarlordDefeated,
			ActivePlayerIndex = 1,
			CurrentPhase = TurnPhase.Main
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0
		};
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		PassAction action = new PassAction
		{
			PlayerIndex = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Game is over"));
	}

	[Fact]
	public void DealDamageEvent_Lethal_To_Warlord_Fires_GameOverEvent_Via_Stack()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 5,
			DefinitionId = "warlord-hero",
			CurrentHitPoints = 2,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "P1"
		};
		ref PlayerState reference = ref span[1];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "P2",
			WarlordInstanceId = 5
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 5,
			Amount = 5,
			DamageType = "magic"
		});
		gameEngine.Resolver.ResolveAll(gameState2);
		Assert.True(gameState2.IsGameOver);
		Assert.Equal(0, gameState2.WinningPlayerIndex);
		Assert.Equal(GameOverReason.WarlordDefeated, gameState2.GameOverReason);
	}
}
