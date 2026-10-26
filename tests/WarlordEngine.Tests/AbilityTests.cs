using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class AbilityTests
{
	[Fact]
	public void SpendOrder_Ability_Spends_Card_And_Deals_Damage_Via_Stack()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "wizard-caster",
			Name = "Wizard Caster",
			Type = CardType.Character,
			Class = CharacterClass.Wizard,
			Level = 2,
			BaseHitPoints = 5,
			ArmorClass = 12,
			Abilities = new _003C_003Ez__ReadOnlySingleElementList<AbilityDefinition>(new AbilityDefinition
			{
				Trigger = AbilityTrigger.SpendOrder,
				Effects = new _003C_003Ez__ReadOnlySingleElementList<IEffect>(new DealDamageEffect(3, "fire")),
				Description = "Spend Order: Deal 3 fire damage to target character."
			})
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "target-dummy",
			Name = "Target Dummy",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			BaseHitPoints = 10,
			ArmorClass = 10
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "wizard-caster",
			CurrentHitPoints = 5,
			CurrentArmorClass = 12,
			CurrentSkill = 4,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "target-dummy",
			CurrentHitPoints = 10,
			CurrentArmorClass = 10,
			CurrentSkill = 0,
			ReadyState = ReadyState.Ready,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			NextInstanceId = 10
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
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1,
			DisplayName = "Player 2"
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			TargetInstanceIds = new int[1] { 2 }
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		ActionResult actionResult2 = gameEngine.Execute(action);
		Assert.True(actionResult2.IsSuccess, string.Join("; ", actionResult2.Errors));
		Assert.Equal(ReadyState.Spent, gameState2.Players[0].Ranks[1][0].ReadyState);
		Assert.Equal(7, gameState2.Players[1].Ranks[1][0].CurrentHitPoints);
	}
}
