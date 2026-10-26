using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class GameEnginePhaseTests
{
	[Fact]
	public void AdvancePhase_Follows_Turn_Structure()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.Ready;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Draw, gameState.CurrentPhase);
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Order, gameState.CurrentPhase);
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.EndOfTurn, gameState.CurrentPhase);
	}

	[Fact]
	public void AdvancePhase_End_Switches_To_Next_Player()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.End;
		gameState.ActivePlayerIndex = 0;
		int turnNumber = gameState.TurnNumber;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.AdvancePhase();
		Assert.Equal(TurnPhase.Ready, gameState.CurrentPhase);
		Assert.Equal(1, gameState.ActivePlayerIndex);
		Assert.Equal(turnNumber + 1, gameState.TurnNumber);
	}

	[Fact]
	public void AdvancePhase_Wraps_Player_Index()
	{
		GameState gameState = EngineTestFactory.CreateMainPhaseState();
		gameState.CurrentPhase = TurnPhase.End;
		gameState.ActivePlayerIndex = 1;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.AdvancePhase();
		Assert.Equal(0, gameState.ActivePlayerIndex);
		Assert.Equal(TurnPhase.Ready, gameState.CurrentPhase);
	}
}
