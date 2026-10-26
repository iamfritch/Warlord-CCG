using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class GameEngineDeathCheckTests
{
	[Fact]
	public void DealDamage_Killing_Blow_Triggers_CharacterDefeatedEvent()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 5);
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 5,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.False(gameState.Players[1].Ranks.ContainsKey(1));
		Assert.Single(gameState.Players[1].DiscardPile);
		Assert.Equal(2, gameState.Players[1].DiscardPile[0].InstanceId);
		Assert.Equal(CardZone.Discard, gameState.Players[1].DiscardPile[0].Zone);
	}

	[Fact]
	public void DealDamage_NonLethal_Does_Not_Trigger_Death()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 10);
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 3,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.True(gameState.Players[1].Ranks.ContainsKey(1));
		Assert.Equal(7, gameState.Players[1].Ranks[1][0].CurrentHitPoints);
		Assert.Empty(gameState.Players[1].DiscardPile);
	}

	[Fact]
	public void Defeated_Warlord_Clears_WarlordInstanceId()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 3);
		gameState.Players[1].WarlordInstanceId = 2;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 10,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.Null(gameState.Players[1].WarlordInstanceId);
	}

	[Fact]
	public void CharacterDefeatedEvent_Uses_With_Pattern_For_Zone_Change()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 1);
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 5,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		CardInstance cardInstance = gameState.Players[1].DiscardPile[0];
		Assert.Equal(CardZone.Discard, cardInstance.Zone);
		Assert.Equal(ReadyState.Spent, cardInstance.ReadyState);
	}

	[Fact]
	public void When_Last_Character_In_Rank1_Dies_Rank2_Shifts_Down_To_Rank1()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 5);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[1].Ranks;
		int num = 1;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = cardInstance;
		ranks[2] = list;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 5,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.Single(gameState.Players[1].DiscardPile);
		Assert.Equal(2, gameState.Players[1].DiscardPile[0].InstanceId);
		Assert.True(gameState.Players[1].Ranks.ContainsKey(1));
		Assert.False(gameState.Players[1].Ranks.ContainsKey(2));
		Assert.Single(gameState.Players[1].Ranks[1]);
		Assert.Equal(20, gameState.Players[1].Ranks[1][0].InstanceId);
		Assert.Equal(1, gameState.Players[1].Ranks[1][0].Rank);
	}

	[Fact]
	public void When_Rank1_Dies_Leaving_1_In_Rank1_And_2_In_Rank2_Ready_Character_Falls_Forward_To_Spent()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 5);
		CardInstance item = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		gameState.Players[1].Ranks[1].Add(item);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 21,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 22,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[1].Ranks;
		int num = 2;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list);
		span[0] = cardInstance;
		span[1] = cardInstance2;
		ranks[2] = list;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 5,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.Equal(2, gameState.Players[1].Ranks[1].Count);
		Assert.Single(gameState.Players[1].Ranks[2]);
		CardInstance cardInstance3 = gameState.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 21 || c.InstanceId == 22);
		Assert.Equal(1, cardInstance3.Rank);
		Assert.Equal(ReadyState.Spent, cardInstance3.ReadyState);
	}

	[Fact]
	public void When_Rank1_Dies_Leaving_1_In_Rank1_And_2_In_Rank2_Spent_Character_Falls_Forward_To_Stunned()
	{
		GameState gameState = EngineTestFactory.CreateCombatState(1, 2, 10, 5);
		CardInstance item = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 1
		};
		gameState.Players[1].Ranks[1].Add(item);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 21,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 22,
			DefinitionId = "test-fighter",
			CurrentHitPoints = 10,
			CurrentArmorClass = 15,
			CurrentSkill = 5,
			ReadyState = ReadyState.Spent,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			Rank = 2
		};
		SortedDictionary<int, List<CardInstance>> ranks = gameState.Players[1].Ranks;
		int num = 2;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list);
		span[0] = cardInstance;
		span[1] = cardInstance2;
		ranks[2] = list;
		GameEngine gameEngine = new GameEngine(gameState, EngineTestFactory.CreateCardDb());
		gameEngine.Resolver.Push(new DealDamageEvent
		{
			TargetInstanceId = 2,
			Amount = 5,
			DamageType = "melee"
		});
		gameEngine.Resolver.ResolveAll(gameState);
		Assert.Equal(2, gameState.Players[1].Ranks[1].Count);
		Assert.Single(gameState.Players[1].Ranks[2]);
		CardInstance cardInstance3 = gameState.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 21 || c.InstanceId == 22);
		Assert.Equal(1, cardInstance3.Rank);
		Assert.Equal(ReadyState.Stunned, cardInstance3.ReadyState);
	}
}
