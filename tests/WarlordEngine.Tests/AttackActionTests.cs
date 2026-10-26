using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class AttackActionTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["warrior"] = new CardDefinition
			{
				DefinitionId = "warrior",
				Name = "Fierce Warrior",
				Type = CardType.Character,
				Class = CharacterClass.Fighter,
				Level = 1,
				ArmorClass = 14,
				BaseHitPoints = 10,
				Attack = new AttackValue
				{
					Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					} },
					RawText = "+3"
				}
			} }
		};
	}

	[Fact]
	public void AttackAction_Valid_Melee_Spends_Attacker_And_Pushes_Damage()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
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
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1][0].ReadyState);
		Assert.Equal(9, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void AttackAction_Spent_Attacker_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
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
			PlayerIndex = 1
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
		GameEngine gameEngine = new GameEngine(state, cardDb);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("spent"));
	}

	[Fact]
	public void AttackAction_On_Opponents_Turn_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
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
		AttackAction action = new AttackAction
		{
			PlayerIndex = 1,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("not Player 1's turn"));
	}

	[Fact]
	public void AttackAction_Attacking_Own_Character_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Board
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Board
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance;
		span2[1] = cardInstance2;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("own character"));
	}

	[Fact]
	public void AttackAction_Out_Of_Range_Melee_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			OwnerPlayerIndex = 0,
			Zone = CardZone.Board
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			OwnerPlayerIndex = 1,
			Zone = CardZone.Board
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[2] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
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
		GameEngine gameEngine = new GameEngine(state, cardDb);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Rank 1"));
	}

	[Fact]
	public void AttackAction_Natural_1_Always_Misses_Even_If_Roll_Plus_Atk_Exceeds_AC()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["hero"] = new CardDefinition
			{
				DefinitionId = "hero",
				Name = "Hero",
				Type = CardType.Character,
				ArmorClass = 5,
				BaseHitPoints = 10,
				Attack = new AttackValue
				{
					Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
					{
						Modifier = 10,
						IsRanged = false
					} },
					RawText = "+10"
				}
			} }
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "hero",
			CurrentHitPoints = 10,
			CurrentArmorClass = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "hero",
			CurrentHitPoints = 10,
			CurrentArmorClass = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
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
			PlayerIndex = 1
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
		gameEngine.SetDiceRoller((int _, int _) => 1);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1][0].ReadyState);
		Assert.Equal(10, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void AttackAction_Natural_20_Always_Hits_Even_If_Roll_Plus_Atk_Below_AC()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["peasant"] = new CardDefinition
				{
					DefinitionId = "peasant",
					Name = "Peasant",
					Type = CardType.Character,
					ArmorClass = 10,
					BaseHitPoints = 10,
					Attack = new AttackValue
					{
						Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
						{
							Modifier = 0,
							IsRanged = false
						} },
						RawText = "+0"
					}
				},
				["golem"] = new CardDefinition
				{
					DefinitionId = "golem",
					Name = "Iron Golem",
					Type = CardType.Character,
					ArmorClass = 35,
					BaseHitPoints = 10,
					Attack = new AttackValue()
				}
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "peasant",
			CurrentHitPoints = 10,
			CurrentArmorClass = 10,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "golem",
			CurrentHitPoints = 10,
			CurrentArmorClass = 35,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
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
			PlayerIndex = 1
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
		gameEngine.SetDiceRoller((int _, int _) => 20);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1][0].ReadyState);
		Assert.True(gameState2.Players[1].Ranks[1][0].CurrentHitPoints < 10);
	}

	[Fact]
	public void AttackAction_Hits_When_Roll_Plus_Atk_Greater_Than_Or_Equal_To_AC()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
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
			PlayerIndex = 1
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
		gameEngine.SetDiceRoller((int _, int _) => 11);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(9, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void AttackAction_Misses_When_Roll_Plus_Atk_Below_AC()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
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
			PlayerIndex = 0
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
			PlayerIndex = 1
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
		gameEngine.SetDiceRoller((int _, int _) => 10);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1][0].ReadyState);
		Assert.Equal(10, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void CardInstance_AttachedItemIds_DeepCopy_Prevents_Shallow_Copy_Mutation()
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior"
		};
		int num = 2;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<int> span = CollectionsMarshal.AsSpan(list);
		span[0] = 10;
		span[1] = 20;
		cardInstance.AttachedItemIds = list;
		CardInstance cardInstance2 = cardInstance;
		CardInstance cardInstance3 = cardInstance2.DeepClone();
		num = 3;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<int> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = 10;
		span2[1] = 20;
		span2[2] = 30;
		cardInstance3.AttachedItemIds = list2;
		Assert.Equal(2, cardInstance2.AttachedItemIds.Count);
		Assert.Equal(3, cardInstance3.AttachedItemIds.Count);
		List<int> attachedItemIds = cardInstance2.AttachedItemIds;
		attachedItemIds.Add(99);
		Assert.Equal(3, cardInstance2.AttachedItemIds.Count);
	}

	[Fact]
	public void Character_Defeated_With_Attached_Items_Moves_Items_To_Discard_Without_Duplicates()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "sword",
			Zone = CardZone.Attached,
			AttachedToInstanceId = 2,
			OwnerPlayerIndex = 1
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "warrior",
			CurrentHitPoints = 1,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		int num = 1;
		List<int> list = new List<int>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = 20;
		cardInstance2.AttachedItemIds = list;
		CardInstance cardInstance3 = cardInstance2;
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warrior",
			CurrentHitPoints = 10,
			CurrentArmorClass = 14,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
		};
		num = 2;
		List<PlayerState> list2 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list2);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance4;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance3;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = cardInstance;
		playerState2.DiscardPile = list5;
		reference2 = playerState2;
		gameState.Players = list2;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		gameEngine.SetDiceRoller((int _, int _) => 15);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		List<CardInstance> discardPile = gameState2.Players[1].DiscardPile;
		Assert.Contains(discardPile, (CardInstance c) => c.InstanceId == 2 && c.Zone == CardZone.Discard);
		Assert.Single(discardPile, (CardInstance c) => c.InstanceId == 20);
		CardInstance cardInstance5 = discardPile.First((CardInstance c) => c.InstanceId == 20);
		Assert.Equal(CardZone.Discard, cardInstance5.Zone);
		Assert.Null(cardInstance5.AttachedToInstanceId);
	}
}
