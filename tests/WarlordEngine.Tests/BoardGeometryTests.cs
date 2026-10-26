using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class BoardGeometryTests
{
	[Theory]
	[InlineData(new object[] { 1, 1, 0 })]
	[InlineData(new object[] { 1, 3, 2 })]
	[InlineData(new object[] { 3, 1, 2 })]
	[InlineData(new object[] { 2, 2, 0 })]
	public void GetDistance_Returns_Absolute_Difference(int a, int b, int expected)
	{
		Assert.Equal(expected, BoardGeometry.GetDistance(a, b));
	}

	[Theory]
	[InlineData(new object[] { 1, 1, 1 })]
	[InlineData(new object[] { 1, 2, 2 })]
	[InlineData(new object[] { 2, 2, 3 })]
	[InlineData(new object[] { 1, 3, 3 })]
	public void GetCrossBoardDistance_Sums_Ranks_Minus_One(int attackerRank, int defenderRank, int expected)
	{
		Assert.Equal(expected, BoardGeometry.GetCrossBoardDistance(attackerRank, defenderRank));
	}

	[Fact]
	public void IsMeleeValid_Both_At_Rank1_Opposing_Players()
	{
		GameState gameState = EngineTestFactory.CreateCombatState();
		CardInstance attacker = gameState.Players[0].Ranks[1][0];
		CardInstance defender = gameState.Players[1].Ranks[1][0];
		Assert.True(BoardGeometry.IsMeleeValid(attacker, defender, gameState));
	}

	[Fact]
	public void IsMeleeValid_Fails_When_Attacker_Not_At_Rank1()
	{
		GameState gameState = EngineTestFactory.CreateCombatState();
		CardInstance cardInstance = gameState.Players[0].Ranks[1][0];
		gameState.Players[0].Ranks.Remove(1);
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[0].Ranks;
		int num = 1;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = cardInstance;
		ranks[2] = list;
		CardInstance defender = gameState.Players[1].Ranks[1][0];
		Assert.False(BoardGeometry.IsMeleeValid(cardInstance, defender, gameState));
	}

	[Fact]
	public void GetValidRangedTargets_Returns_Targets_Within_Range()
	{
		GameState gameState = EngineTestFactory.CreateCombatState();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 5,
			CurrentArmorClass = 12,
			CurrentSkill = 3,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[1].Ranks;
		int num = 1;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = cardInstance;
		ranks[3] = list;
		CardInstance attacker = gameState.Players[0].Ranks[1][0];
		List<CardInstance> validRangedTargets = BoardGeometry.GetValidRangedTargets(attacker, 1, gameState);
		Assert.Single(validRangedTargets);
		Assert.Equal(2, validRangedTargets[0].InstanceId);
		List<CardInstance> validRangedTargets2 = BoardGeometry.GetValidRangedTargets(attacker, 3, gameState);
		Assert.Equal(2, validRangedTargets2.Count);
	}
}
