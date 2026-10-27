using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class MultiTargetValidationTests
{
	private const string PackTacticsText = "Spend Order: Your two target characters attack as normal.";

	private static CardInstance Char(int id, int owner, int rank)
	{
		return new CardInstance
		{
			InstanceId = id,
			DefinitionId = "char",
			OwnerPlayerIndex = owner,
			Rank = rank,
			Zone = CardZone.Board,
			CurrentHitPoints = 3,
			ReadyState = ReadyState.Ready
		};
	}

	private static (GameEngine Engine, GameState State) Build()
	{
		CardDefinition source = new CardDefinition
		{
			DefinitionId = "src",
			Name = "Pack Tactics Source",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3,
			Abilities = RulesParser.Parse(PackTacticsText, "Pack Tactics")
		};
		CardDefinition plain = new CardDefinition
		{
			DefinitionId = "char",
			Name = "Plain",
			Type = CardType.Character,
			Level = 1,
			BaseHitPoints = 3
		};
		CardDatabase db = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { ["src"] = source, ["char"] = plain }
		};
		CardInstance src = Char(1, 0, 1) with { DefinitionId = "src" };
		PlayerState p0 = new PlayerState { PlayerIndex = 0, DisplayName = "P0" };
		p0.Ranks = new SortedDictionary<int, List<CardInstance>> { [1] = new List<CardInstance> { src, Char(2, 0, 1), Char(3, 0, 1) } };
		PlayerState p1 = new PlayerState { PlayerIndex = 1, DisplayName = "P1" };
		p1.Ranks = new SortedDictionary<int, List<CardInstance>> { [1] = new List<CardInstance> { Char(4, 1, 1) } };
		GameState state = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 10,
			Players = new List<PlayerState> { p0, p1 }
		};
		return (new GameEngine(state, db), state);
	}

	private static UseAbilityAction Use(params int[] targets)
	{
		return new UseAbilityAction { PlayerIndex = 0, SourceInstanceId = 1, AbilityIndex = 0, TargetInstanceIds = targets };
	}

	[Fact]
	public void ParseRequiredTargets_ReadsCountAndOwnership()
	{
		(int Count, bool MustBeFriendly)? parsed = RulesParser.ParseRequiredTargets("Your two target characters attack as normal.");
		Assert.True(parsed.HasValue);
		Assert.Equal(2, parsed.Value.Count);
		Assert.True(parsed.Value.MustBeFriendly);
		Assert.Null(RulesParser.ParseRequiredTargets("Deal 3 damage to target character."));
	}

	[Fact]
	public void PackTactics_TwoDistinctFriendlyTargets_Valid()
	{
		(GameEngine engine, _) = Build();
		ActionResult result = engine.CanExecute(Use(2, 3));
		Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
	}

	[Fact]
	public void PackTactics_WrongTargetCount_Fails()
	{
		(GameEngine engine, _) = Build();
		Assert.False(engine.CanExecute(Use(2)).IsSuccess);
		Assert.False(engine.CanExecute(Use(2, 3, 1)).IsSuccess);
	}

	[Fact]
	public void PackTactics_DuplicateTarget_Fails()
	{
		(GameEngine engine, _) = Build();
		ActionResult result = engine.CanExecute(Use(2, 2));
		Assert.False(result.IsSuccess);
		Assert.Contains(result.Errors, (string e) => e.Contains("different character"));
	}

	[Fact]
	public void PackTactics_EnemyOrMissingTarget_FailsWithoutMutatingState()
	{
		(GameEngine engine, GameState state) = Build();
		Assert.False(engine.CanExecute(Use(2, 4)).IsSuccess);
		Assert.False(engine.CanExecute(Use(2, 999)).IsSuccess);
		ActionResult executed = engine.Execute(Use(2, 4));
		Assert.False(executed.IsSuccess);
		Assert.All(state.Players[0].Ranks[1], (CardInstance c) => Assert.Equal(ReadyState.Ready, c.ReadyState));
		Assert.Equal(3, state.Players[1].Ranks[1][0].CurrentHitPoints);
	}

	[Fact]
	public void PackTactics_SingleReadyCharacter_NotOfferedAsLegalAction()
	{
		(GameEngine engine, GameState state) = Build();
		state.Players[0].Ranks[1][1] = state.Players[0].Ranks[1][1] with { ReadyState = ReadyState.Spent };
		state.Players[0].Ranks[1][2] = state.Players[0].Ranks[1][2] with { ReadyState = ReadyState.Spent };
		IReadOnlyList<IPlayerAction> legal = engine.GetLegalActions(state, 0);
		Assert.DoesNotContain(legal.OfType<UseAbilityAction>(), (UseAbilityAction a) => a.SourceInstanceId == 1 && a.TargetInstanceIds.Length == 1);
	}
}
