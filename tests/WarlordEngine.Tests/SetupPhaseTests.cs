using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class SetupPhaseTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["warlord-hero"] = new CardDefinition
				{
					DefinitionId = "warlord-hero",
					Name = "Warlord Hero",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 3,
					ArmorClass = 15,
					BaseHitPoints = 12,
					IsWarlord = true,
					Faction = "Free Kingdoms"
				},
				["level-2-troop"] = new CardDefinition
				{
					DefinitionId = "level-2-troop",
					Name = "Veteran Sergeant",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 2,
					ArmorClass = 14,
					BaseHitPoints = 8,
					Faction = "Free Kingdoms"
				},
				["level-1-troop"] = new CardDefinition
				{
					DefinitionId = "level-1-troop",
					Name = "Foot Soldier",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 6,
					Faction = "Free Kingdoms"
				}
			}
		};
	}

	[Fact]
	public void DeployStartingArmy_Valid_Composition_Deploys_Warlord_And_Advances_When_Both_Deployed()
	{
		CardDatabase cardDb = CreateTestCardDb();
		List<CardInstance> hand = new List<CardInstance>
		{
			new CardInstance
			{
				InstanceId = 1,
				DefinitionId = "warlord-hero",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 2,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 3,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 4,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 5,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 6,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			}
		};
		List<CardInstance> hand2 = new List<CardInstance>
		{
			new CardInstance
			{
				InstanceId = 10,
				DefinitionId = "warlord-hero",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			},
			new CardInstance
			{
				InstanceId = 11,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			},
			new CardInstance
			{
				InstanceId = 12,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			},
			new CardInstance
			{
				InstanceId = 13,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			},
			new CardInstance
			{
				InstanceId = 14,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			},
			new CardInstance
			{
				InstanceId = 15,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 1
			}
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Setup
		};
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			DisplayName = "P1",
			Hand = hand
		};
		span[1] = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "P2",
			Hand = hand2
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		DeployStartingArmyAction action = new DeployStartingArmyAction
		{
			PlayerIndex = 0,
			Deployments = new _003C_003Ez__ReadOnlyArray<StartingCharacterDeployment>(new StartingCharacterDeployment[6]
			{
				new StartingCharacterDeployment(1, 3),
				new StartingCharacterDeployment(2, 2),
				new StartingCharacterDeployment(3, 2),
				new StartingCharacterDeployment(4, 1),
				new StartingCharacterDeployment(5, 1),
				new StartingCharacterDeployment(6, 1)
			})
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		gameEngine.Execute(action);
		Assert.Equal(1, gameState2.Players[0].WarlordInstanceId);
		Assert.Equal(TurnPhase.Setup, gameState2.CurrentPhase);
		gameState2.ActivePlayerIndex = 1;
		DeployStartingArmyAction action2 = new DeployStartingArmyAction
		{
			PlayerIndex = 1,
			Deployments = new _003C_003Ez__ReadOnlyArray<StartingCharacterDeployment>(new StartingCharacterDeployment[6]
			{
				new StartingCharacterDeployment(10, 3),
				new StartingCharacterDeployment(11, 2),
				new StartingCharacterDeployment(12, 2),
				new StartingCharacterDeployment(13, 1),
				new StartingCharacterDeployment(14, 1),
				new StartingCharacterDeployment(15, 1)
			})
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		gameEngine.Execute(action2);
		Assert.Equal(10, gameState2.Players[1].WarlordInstanceId);
		Assert.Equal(TurnPhase.Ready, gameState2.CurrentPhase);
	}

	[Fact]
	public void DeployStartingArmy_Incorrect_Rank_Composition_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		List<CardInstance> hand = new List<CardInstance>
		{
			new CardInstance
			{
				InstanceId = 1,
				DefinitionId = "warlord-hero",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 2,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 3,
				DefinitionId = "level-2-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 4,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 5,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			},
			new CardInstance
			{
				InstanceId = 6,
				DefinitionId = "level-1-troop",
				Zone = CardZone.Hand,
				OwnerPlayerIndex = 0
			}
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
		span[0] = new PlayerState
		{
			PlayerIndex = 0,
			Hand = hand
		};
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
			Deployments = new _003C_003Ez__ReadOnlyArray<StartingCharacterDeployment>(new StartingCharacterDeployment[6]
			{
				new StartingCharacterDeployment(1, 1),
				new StartingCharacterDeployment(2, 2),
				new StartingCharacterDeployment(3, 2),
				new StartingCharacterDeployment(4, 1),
				new StartingCharacterDeployment(5, 1),
				new StartingCharacterDeployment(6, 1)
			})
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("must be deployed in Rank 3"));
	}

	[Fact]
	public void DeployStartingArmy_Wrong_Phase_Is_Rejected()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "warlord-hero",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState
		{
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
		DeployStartingArmyAction action = new DeployStartingArmyAction
		{
			PlayerIndex = 0,
			Deployments = new _003C_003Ez__ReadOnlySingleElementList<StartingCharacterDeployment>(new StartingCharacterDeployment(1, 3))
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Setup phase"));
	}
}
