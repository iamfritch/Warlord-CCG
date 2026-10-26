using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class ReactConditionAndTimingTests
{
	[Fact]
	public void RulesParser_Parses_AlraynGaunt_TimingAndScope()
	{
		List<AbilityDefinition> list = RulesParser.Parse("React: After Alrayn Gaunt moves: Perform a melee strike.", "Alrayn Gaunt");
		Assert.Single(list);
		AbilityDefinition abilityDefinition = list[0];
		Assert.Equal(AbilityTrigger.React, abilityDefinition.Trigger);
		Assert.Equal(ReactTiming.AfterMove, abilityDefinition.Timing);
		Assert.Equal(TargetScope.Self, abilityDefinition.TargetScope);
	}

	[Fact]
	public void RulesParser_Parses_TyrenRuskin_TimingAndScope()
	{
		List<AbilityDefinition> list = RulesParser.Parse("Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one.", "Tyren Ruskin");
		Assert.Single(list);
		AbilityDefinition abilityDefinition = list[0];
		Assert.Equal(AbilityTrigger.SpendReact, abilityDefinition.Trigger);
		Assert.Equal(ReactTiming.BeforeSuffersWounds, abilityDefinition.Timing);
		Assert.Equal(TargetScope.SameRank, abilityDefinition.TargetScope);
	}

	[Fact]
	public void AlraynGaunt_Ability_NotLegalDuringAttackReactWindow_OnlyAfterMove()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "alrayn_gaunt",
			Name = "Alrayn Gaunt",
			Type = CardType.Character,
			Faction = "Mercenary",
			Level = 5,
			ArmorClass = 16,
			BaseHitPoints = 3,
			RulesText = "React: After Alrayn Gaunt moves: Perform a melee strike.",
			Abilities = RulesParser.Parse("React: After Alrayn Gaunt moves: Perform a melee strike.", "Alrayn Gaunt")
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "enemy-warlord",
			Name = "Enemy Warlord",
			Type = CardType.Character,
			Faction = "Dwarf",
			Level = 4,
			ArmorClass = 15,
			BaseHitPoints = 4
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2
			}
		};
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0,
			AutoPassWhenNoReactsAvailable = false
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
			DefinitionId = "alrayn_gaunt",
			CurrentHitPoints = 3,
			CurrentArmorClass = 16,
			ReadyState = ReadyState.Ready,
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
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
			DefinitionId = "enemy-warlord",
			CurrentHitPoints = 4,
			CurrentArmorClass = 15,
			ReadyState = ReadyState.Ready,
			OwnerPlayerIndex = 1,
			Rank = 1,
			Zone = CardZone.Board
		};
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
		gameEngine.ExecuteAction(action);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		Assert.DoesNotContain(legalActions, (IPlayerAction a) => a is UseAbilityAction useAbilityAction && useAbilityAction.SourceInstanceId == 1);
		UseAbilityAction action2 = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 1 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action2, gameState2);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("move", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void TyrenRuskin_Ability_OnlyLegalWhenCharacterInSameRankSuffersWounds()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "tyren_ruskin",
			Name = "Tyren Ruskin",
			Type = CardType.Character,
			Faction = "Mercenary",
			Level = 2,
			ArmorClass = 9,
			BaseHitPoints = 2,
			RulesText = "Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one.",
			Abilities = RulesParser.Parse("Spend React: Before target character in this rank suffers wounds, wound Tyren: Reduce the wounds the target would suffer by one.", "Tyren Ruskin")
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "frontline",
			Name = "Frontline Fighter",
			Type = CardType.Character,
			Faction = "Mercenary",
			Level = 1,
			ArmorClass = 12,
			BaseHitPoints = 2
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "rank2_friend",
			Name = "Rank 2 Ally",
			Type = CardType.Character,
			Faction = "Mercenary",
			Level = 2,
			ArmorClass = 11,
			BaseHitPoints = 2
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		GameState gameState = new GameState
		{
			CurrentPhase = TurnPhase.Main,
			ActivePlayerIndex = 0,
			AutoPassWhenNoReactsAvailable = false
		};
		int num = 1;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		ref PlayerState reference = ref CollectionsMarshal.AsSpan(list)[0];
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
			InstanceId = 10,
			DefinitionId = "frontline",
			CurrentHitPoints = 2,
			CurrentArmorClass = 12,
			ReadyState = ReadyState.Ready,
			OwnerPlayerIndex = 0,
			Rank = 1,
			Zone = CardZone.Board
		};
		sortedDictionary[1] = list2;
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list3);
		span[0] = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = "tyren_ruskin",
			CurrentHitPoints = 2,
			CurrentArmorClass = 9,
			ReadyState = ReadyState.Ready,
			OwnerPlayerIndex = 0,
			Rank = 2,
			Zone = CardZone.Board
		};
		span[1] = new CardInstance
		{
			InstanceId = 30,
			DefinitionId = "rank2_friend",
			CurrentHitPoints = 2,
			CurrentArmorClass = 11,
			ReadyState = ReadyState.Ready,
			OwnerPlayerIndex = 0,
			Rank = 2,
			Zone = CardZone.Board
		};
		sortedDictionary[2] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		gameState2.Priority.IsReactWindowOpen = true;
		gameState2.PendingEvents.Clear();
		gameState2.PendingEvents.Add(new DeclareAttackEvent
		{
			AttackerInstanceId = 99,
			DefenderInstanceId = 10
		});
		IReadOnlyList<IPlayerAction> legalActions = gameEngine.GetLegalActions(gameState2, 0);
		Assert.DoesNotContain(legalActions, (IPlayerAction a) => a is UseAbilityAction useAbilityAction && useAbilityAction.SourceInstanceId == 20);
		gameState2.PendingEvents.Clear();
		gameState2.PendingEvents.Add(new DealDamageEvent
		{
			TargetInstanceId = 10,
			Amount = 1,
			DamageType = "physical"
		});
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 20,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 10 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action, gameState2);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("same rank", StringComparison.OrdinalIgnoreCase));
		gameState2.PendingEvents.Clear();
		gameState2.PendingEvents.Add(new DealDamageEvent
		{
			TargetInstanceId = 30,
			Amount = 1,
			DamageType = "physical"
		});
		UseAbilityAction action2 = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 20,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 30 }
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2, gameState2);
		Assert.True(actionResult2.IsSuccess);
		IReadOnlyList<IPlayerAction> legalActions2 = gameEngine.GetLegalActions(gameState2, 0);
		Assert.Contains(legalActions2, (IPlayerAction a) => a is UseAbilityAction { SourceInstanceId: 20 } useAbilityAction && useAbilityAction.TargetInstanceIds.Contains(30));
	}
}
