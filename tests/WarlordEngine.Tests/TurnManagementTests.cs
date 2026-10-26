using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class TurnManagementTests
{
	[Fact]
	public void GameState_Initializes_TurnState_To_Turn1_SetupPhase_Player0()
	{
		GameState gameState = new GameState();
		Assert.NotNull(gameState.TurnState);
		Assert.Equal(1, gameState.TurnState.TurnNumber);
		Assert.Equal(TurnPhase.Setup, gameState.TurnState.CurrentPhase);
		Assert.Equal(0, gameState.TurnState.ActivePlayerIndex);
		Assert.Equal(0, gameState.TurnState.Priority.ConsecutivePasses);
	}

	[Fact]
	public void PassAction_Executes_TurnState_RecordPass()
	{
		GameState gameState = new GameState();
		PassAction passAction = new PassAction
		{
			PlayerIndex = 0
		};
		passAction.Execute(gameState);
		Assert.True(gameState.TurnState.Priority.HasPassed);
		Assert.Equal(1, gameState.TurnState.Priority.ConsecutivePasses);
	}

	[Fact]
	public void ReadyPhase_Transitions_Stunned_To_Spent_And_Spent_To_Ready()
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "card1",
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "card2",
			ReadyState = ReadyState.Stunned,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "card3",
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.End
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
		int num2 = 3;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance;
		span2[1] = cardInstance2;
		span2[2] = cardInstance3;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, new CardDatabase());
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Ready, gameState2.CurrentPhase);
		List<CardInstance> source = gameState2.Players[0].Ranks[1];
		CardInstance cardInstance4 = source.First((CardInstance c) => c.InstanceId == 1);
		CardInstance cardInstance5 = source.First((CardInstance c) => c.InstanceId == 2);
		CardInstance cardInstance6 = source.First((CardInstance c) => c.InstanceId == 3);
		Assert.Equal(ReadyState.Ready, cardInstance4.ReadyState);
		Assert.Equal(ReadyState.Spent, cardInstance5.ReadyState);
		Assert.Equal(ReadyState.Ready, cardInstance6.ReadyState);
	}

	[Fact]
	public void DrawPhase_Draws_Up_To_5_Cards_From_Deck_To_Hand()
	{
		List<CardInstance> hand = new List<CardInstance>
		{
			new CardInstance
			{
				InstanceId = 1,
				DefinitionId = "c1",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 2,
				DefinitionId = "c2",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			}
		};
		List<CardInstance> deck = new List<CardInstance>
		{
			new CardInstance
			{
				InstanceId = 3,
				DefinitionId = "c3",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 4,
				DefinitionId = "c4",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 5,
				DefinitionId = "c5",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 6,
				DefinitionId = "c6",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 7,
				DefinitionId = "c7",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			}
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Ready
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1",
			Hand = hand,
			Deck = deck
		};
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, new CardDatabase());
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Draw, gameState2.CurrentPhase);
		PlayerState playerState = gameState2.Players[0];
		Assert.Equal(5, playerState.Hand.Count);
		Assert.Equal(2, playerState.Deck.Count);
		Assert.All(playerState.Hand, (CardInstance c) =>
		{
			Assert.Equal(CardZone.Hand, c.Zone);
		});
	}

	[Fact]
	public void DrawPhase_Does_Not_Draw_When_Hand_Has_5_Or_More_Cards()
	{
		List<CardInstance> hand = (from i in Enumerable.Range(1, 5)
			select new CardInstance
			{
				InstanceId = i,
				DefinitionId = $"c{i}",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			}).ToList();
		List<CardInstance> deck = (from i in Enumerable.Range(6, 5)
			select new CardInstance
			{
				InstanceId = i,
				DefinitionId = $"c{i}",
				Zone = CardZone.Deck,
				OwnerPlayerIndex = 0
			}).ToList();
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Ready
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			Hand = hand,
			Deck = deck
		};
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, new CardDatabase());
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Draw, gameState2.CurrentPhase);
		Assert.Equal(5, gameState2.Players[0].Hand.Count);
		Assert.Equal(5, gameState2.Players[0].Deck.Count);
	}

	[Fact]
	public void InitiativeRoll_Contest_Determines_Decree_Priority_Winner()
	{
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Draw
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
		GameState gameState2 = gameState;
		int rollCount = 0;
		GameEngine gameEngine = new GameEngine(gameState2, new CardDatabase(), (int count, int sides) =>
		{
			rollCount++;
			return (rollCount != 1) ? 18 : 12;
		});
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Main, gameState2.CurrentPhase);
		Assert.NotNull<(int, int, int)>(gameEngine.LastInitiativeRoll);
		Assert.Equal(1, gameEngine.LastInitiativeRoll.Value.WinnerIndex);
		Assert.Equal(12, gameEngine.LastInitiativeRoll.Value.Roll0);
		Assert.Equal(18, gameEngine.LastInitiativeRoll.Value.Roll1);
		Assert.Equal(1, gameState2.ActivePlayerIndex);
	}

	[Fact]
	public void DecreePhase_SingleOrder_Alternates_Priority_Between_Players()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["fighter"] = new CardDefinition
			{
				DefinitionId = "fighter",
				Name = "Fighter",
				Type = CardType.Character,
				Class = CharacterClass.Fighter,
				Level = 1,
				ArmorClass = 13,
				BaseHitPoints = 5
			} }
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
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "fighter",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		playerState.Hand = list2;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "fighter",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 1
		};
		playerState2.Hand = list3;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(1, gameState2.ActivePlayerIndex);
		Assert.Equal(1, gameState2.Priority.ActivePlayerIndex);
		Assert.Equal(0, gameState2.Priority.ConsecutivePasses);
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 1,
			CardInstanceId = 20,
			TargetRank = 1
		};
		ActionResult actionResult2 = gameEngine.Execute(action2);
		Assert.True(actionResult2.IsSuccess);
		Assert.Equal(0, gameState2.ActivePlayerIndex);
		Assert.Equal(0, gameState2.Priority.ActivePlayerIndex);
		Assert.Equal(0, gameState2.Priority.ConsecutivePasses);
	}

	[Fact]
	public void DecreePhase_Both_Players_Pass_Consecutively_Advances_To_Next_Turn()
	{
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
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "f1",
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, new CardDatabase());
		ActionResult actionResult = gameEngine.Execute(new PassAction
		{
			PlayerIndex = 0
		});
		Assert.True(actionResult.IsSuccess);
		Assert.Equal(1, gameState2.Priority.ConsecutivePasses);
		Assert.Equal(1, gameState2.ActivePlayerIndex);
		Assert.Equal(TurnPhase.Main, gameState2.CurrentPhase);
		ActionResult actionResult2 = gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		Assert.True(actionResult2.IsSuccess);
		Assert.Equal(TurnPhase.Ready, gameState2.CurrentPhase);
		Assert.Equal(2, gameState2.TurnNumber);
		Assert.Equal(0, gameState2.Priority.ConsecutivePasses);
		Assert.Equal(ReadyState.Ready, gameState2.Players[0].Ranks[1][0].ReadyState);
	}

	[Fact]
	public void AttackResolution_Fires_OnAttackResolved_With_Accurate_Dice_Math()
	{
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["hero"] = new CardDefinition
			{
				DefinitionId = "hero",
				Name = "Hero",
				Type = CardType.Character,
				Class = CharacterClass.Fighter,
				Level = 1,
				ArmorClass = 15,
				BaseHitPoints = 10,
				Attack = new AttackValue
				{
					RawText = "+3",
					Strikes = new List<StrikeDescriptor> { new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					} }
				}
			} }
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
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "hero",
			CurrentArmorClass = 15,
			CurrentHitPoints = 10,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
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
		CollectionsMarshal.AsSpan(list3)[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "hero",
			CurrentArmorClass = 15,
			CurrentHitPoints = 10,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 14);
		AttackResolutionDetails capturedDetails = null;
		gameEngine.OnAttackResolved = (AttackResolutionDetails d) =>
		{
			capturedDetails = d;
		};
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.NotNull(capturedDetails);
		Assert.Equal(14, capturedDetails.Roll);
		Assert.Equal(3, capturedDetails.StrikeBonus);
		Assert.Equal(17, capturedDetails.TotalRoll);
		Assert.Equal(15, capturedDetails.DefenderArmorClass);
		Assert.True(capturedDetails.IsHit);
		Assert.False(capturedDetails.IsCritical);
		Assert.True(capturedDetails.DamageDealt > 0);
		Assert.Equal(1, gameState2.ActivePlayerIndex);
	}
}
