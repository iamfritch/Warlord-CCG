using System.Collections.Generic;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class GameEngineValidationTests
{
	[Fact]
	public void Deploy_On_Opponents_Turn_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 1,
			DefinitionId = "test-fighter",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("not Player 1's turn"));
	}

	[Fact]
	public void Deploy_During_Wrong_Phase_Is_Rejected()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.Ready;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Main phase"));
	}

	[Fact]
	public void Deploy_Unknown_DefinitionId_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "nonexistent-card",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("not found"));
	}

	[Fact]
	public void Deploy_Non_Character_Definition_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-action",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Action") && e.Contains("not a Character"));
	}

	[Fact]
	public void Deploy_To_Rank_Beyond_Occupied_Front_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Rank 1 must be occupied"));
	}

	[Fact]
	public void Deploy_To_Rank2_Exceeding_Rank1_Count_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		ActionResult actionResult = gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		Assert.True(actionResult.IsSuccess);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		ActionResult actionResult2 = gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 2
		});
		Assert.True(actionResult2.IsSuccess);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		ActionResult actionResult3 = gameEngine.CanExecute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 2
		});
		Assert.False(actionResult3.IsSuccess);
		Assert.Contains(actionResult3.Errors, (string e) => e.Contains("cannot have more characters"));
	}

	[Fact]
	public void Deploy_To_Invalid_Rank_Zero_Is_Rejected()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 0
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("invalid") && e.Contains("1-based"));
	}

	[Fact]
	public void Valid_Deploy_To_Rank_1_Succeeds()
	{
		GameState state = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(state, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Empty(actionResult.Errors);
	}

	[Fact]
	public void Multiple_Errors_Are_Collected()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.End;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		DeployCharacterAction action = new DeployCharacterAction
		{
			PlayerIndex = 1,
			DefinitionId = "test-fighter",
			TargetRank = 1
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.True(actionResult.Errors.Count >= 2, "Expected at least 2 errors (wrong player + wrong phase)");
	}

	[Fact]
	public void Execute_Illegal_Deploy_Does_Not_Mutate_State()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.Ready;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		int count = gameState.Players[0].Hand.Count;
		int count2 = gameState.Players[0].Ranks.Count;
		ActionResult actionResult = gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		Assert.False(actionResult.IsSuccess);
		Assert.Equal(count, gameState.Players[0].Hand.Count);
		Assert.Equal(count2, gameState.Players[0].Ranks.Count);
	}

	[Fact]
	public void Execute_Valid_Deploy_Mints_Instance_And_Places_On_Board()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		ActionResult actionResult = gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		Assert.True(actionResult.IsSuccess);
		Assert.Empty(gameState.Players[0].Hand);
		Assert.True(gameState.Players[0].Ranks.ContainsKey(1));
		Assert.Single(gameState.Players[0].Ranks[1]);
		CardInstance cardInstance = gameState.Players[0].Ranks[1][0];
		Assert.Equal("test-fighter", cardInstance.DefinitionId);
		Assert.Equal(CardZone.Board, cardInstance.Zone);
		Assert.Equal(0, cardInstance.OwnerPlayerIndex);
	}

	[Fact]
	public void Execute_Deploy_Hydrates_Stats_From_Definition()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		CardInstance cardInstance = gameState.Players[0].Ranks[1][0];
		Assert.Equal(10, cardInstance.CurrentHitPoints);
		Assert.Equal(15, cardInstance.CurrentArmorClass);
		Assert.Equal(5, cardInstance.CurrentSkill);
		Assert.Equal(ReadyState.Ready, cardInstance.ReadyState);
	}

	[Fact]
	public void Execute_Deploy_Assigns_Sequential_InstanceIds()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.NextInstanceId = 100;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		gameEngine.Execute(new DeployCharacterAction
		{
			PlayerIndex = 0,
			DefinitionId = "test-fighter",
			TargetRank = 1
		});
		List<CardInstance> list = gameState.Players[0].Ranks[1];
		Assert.Equal(2, list.Count);
		Assert.Equal(100, list[0].InstanceId);
		Assert.Equal(101, list[1].InstanceId);
		Assert.Equal(102, gameState.NextInstanceId);
	}
}
