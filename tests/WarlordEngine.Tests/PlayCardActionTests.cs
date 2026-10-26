using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class PlayCardActionTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["fighter-card"] = new CardDefinition
				{
					DefinitionId = "fighter-card",
					Name = "Fighter Hero",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 15,
					BaseHitPoints = 10
				},
				["magic-sword"] = new CardDefinition
				{
					DefinitionId = "magic-sword",
					Name = "Magic Sword",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1
				},
				["fireball-action"] = new CardDefinition
				{
					DefinitionId = "fireball-action",
					Name = "Fireball",
					Type = CardType.Action,
					Level = 1,
					Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
					{
						Trigger = AbilityTrigger.Order,
						Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DealDamageEffect(4, "fire")),
						Description = "Order: Deal 4 fire damage."
					})
				}
			}
		};
	}

	[Fact]
	public void PlayCard_Character_Moves_From_Hand_To_Board_At_TargetRank()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "fighter-card",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
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
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		playerState.Hand = list2;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.Empty(gameState2.Players[0].Hand);
		Assert.Single(gameState2.Players[0].Ranks[1]);
		CardInstance cardInstance2 = gameState2.Players[0].Ranks[1][0];
		Assert.Equal(10, cardInstance2.InstanceId);
		Assert.Equal(CardZone.Board, cardInstance2.Zone);
	}

	[Fact]
	public void PlayCard_Item_Attaches_To_TargetCharacter_And_Updates_AttachedItemIds()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fighter-card",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "magic-sword",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
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
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance2;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 20,
			TargetCharacterId = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.Empty(gameState2.Players[0].Hand);
		Assert.Contains(20, gameState2.Players[0].Ranks[1][0].AttachedItemIds);
	}

	[Fact]
	public void PlayCard_Action_Moves_To_DiscardPile_And_Triggers_Ability()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 30,
			DefinitionId = "fireball-action",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "fighter-card",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
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
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "Player 1"
		};
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		playerState.Hand = list2;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary[1] = list3;
		playerState2.Ranks = sortedDictionary;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 30,
			TargetCharacterId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.Empty(gameState2.Players[0].Hand);
		Assert.Single(gameState2.Players[0].DiscardPile);
		Assert.Equal(30, gameState2.Players[0].DiscardPile[0].InstanceId);
		Assert.Equal(6, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void PlayCard_Not_In_Hand_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
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
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 999,
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("not in Player 0's hand"));
	}

	[Fact]
	public void PlayCard_During_Wrong_Phase_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "fighter-card",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Ready
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		playerState.Hand = list2;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Main phase"));
	}
}
