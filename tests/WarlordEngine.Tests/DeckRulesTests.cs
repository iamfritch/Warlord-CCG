using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DeckRulesTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["fk-warlord"] = new CardDefinition
				{
					DefinitionId = "fk-warlord",
					Name = "Free Kingdoms Commander",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 15,
					BaseHitPoints = 12,
					IsWarlord = true,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Free Kingdoms" }
				},
				["fk-troop"] = new CardDefinition
				{
					DefinitionId = "fk-troop",
					Name = "Free Kingdoms Soldier",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 6,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Free Kingdoms" }
				},
				["fk-troop-lvl2"] = new CardDefinition
				{
					DefinitionId = "fk-troop-lvl2",
					Name = "Free Kingdoms Sergeant",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 2,
					ArmorClass = 14,
					BaseHitPoints = 8,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Free Kingdoms" }
				},
				["dev-troop"] = new CardDefinition
				{
					DefinitionId = "dev-troop",
					Name = "Deverenian Veteran",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 2,
					ArmorClass = 13,
					BaseHitPoints = 7,
					Faction = "Deverenian",
					Traits = new List<string> { "Deverenian" }
				},
				["merc-troop"] = new CardDefinition
				{
					DefinitionId = "merc-troop",
					Name = "Mercenary Crossbowman",
					Type = CardType.Character,
					Class = CharacterClass.Rogue,
					Level = 2,
					ArmorClass = 12,
					BaseHitPoints = 5,
					Faction = "Mercenary",
					Traits = new List<string> { "Mercenary" }
				}
			}
		};
	}

	[Fact]
	public void Deck_Validate_Rejects_Deck_Under_50_Cards()
	{
		CardDatabase cardDb = CreateTestCardDb();
		List<string> list = new List<string>();
		for (int i = 0; i < 40; i++)
		{
			list.Add("fk-troop");
		}
		Deck deck = new Deck
		{
			DeckName = "Small Deck",
			WarlordDefinitionId = "fk-warlord",
			CardDefinitionIds = list
		};
		ActionResult actionResult = deck.Validate(cardDb);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("below the minimum required size of 50"));
	}

	[Fact]
	public void Deck_Validate_Rejects_More_Than_3_Copies_Of_Same_Card()
	{
		CardDatabase cardDb = CreateTestCardDb();
		List<string> list = new List<string>();
		for (int i = 0; i < 4; i++)
		{
			list.Add("fk-troop");
		}
		for (int j = 0; j < 46; j++)
		{
			list.Add("merc-troop");
		}
		Deck deck = new Deck
		{
			DeckName = "Illegal Copy Deck",
			WarlordDefinitionId = "fk-warlord",
			CardDefinitionIds = list
		};
		ActionResult actionResult = deck.Validate(cardDb);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("exceeds the maximum limit of 3 copies"));
	}

	[Fact]
	public void ValidateDeployStartingArmy_Rejects_Starting_Character_With_Mismatching_Faction()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fk-warlord",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "fk-troop-lvl2",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "fk-troop-lvl2",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 4,
			DefinitionId = "fk-troop",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance5 = new CardInstance
		{
			InstanceId = 5,
			DefinitionId = "fk-troop",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance6 = new CardInstance
		{
			InstanceId = 6,
			DefinitionId = "dev-troop",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Setup
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
		int num2 = 6;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance;
		span2[1] = cardInstance2;
		span2[2] = cardInstance3;
		span2[3] = cardInstance4;
		span2[4] = cardInstance5;
		span2[5] = cardInstance6;
		playerState.Hand = list2;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		DeployStartingArmyAction action = new DeployStartingArmyAction
		{
			PlayerIndex = 0,
			Deployments = new List<StartingCharacterDeployment>
			{
				new StartingCharacterDeployment(1, 3),
				new StartingCharacterDeployment(2, 2),
				new StartingCharacterDeployment(3, 2),
				new StartingCharacterDeployment(4, 1),
				new StartingCharacterDeployment(5, 1),
				new StartingCharacterDeployment(6, 1)
			}
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("does not match Warlord faction"));
	}

	[Fact]
	public void PlayCardAction_Applies_Loyalty_Penalty_Stunned_For_NonMatching_Faction()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fk-warlord",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "dev-troop",
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
			WarlordInstanceId = 1
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
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetRank = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		gameEngine.Execute(action);
		Assert.Equal(ReadyState.Stunned, gameState2.Players[0].Ranks[2].First((CardInstance c) => c.InstanceId == 10).ReadyState);
	}

	[Fact]
	public void PlayCardAction_Deploys_Ready_For_Matching_Faction_Or_Mercenary()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "fk-warlord",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "fk-troop-lvl2",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = "merc-troop",
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
			WarlordInstanceId = 1
		};
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list3);
		span3[0] = cardInstance;
		span3[1] = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = "fk-warlord",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		gameEngine.Execute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetRank = 2
		});
		Assert.Equal(ReadyState.Ready, gameState2.Players[0].Ranks[2].First((CardInstance c) => c.InstanceId == 10).ReadyState);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		gameEngine.Execute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 11,
			TargetRank = 2
		});
		Assert.Equal(ReadyState.Ready, gameState2.Players[0].Ranks[2].First((CardInstance c) => c.InstanceId == 11).ReadyState);
	}
}
