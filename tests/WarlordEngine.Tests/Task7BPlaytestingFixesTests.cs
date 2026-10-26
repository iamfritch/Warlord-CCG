using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordCli;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using WarlordEngine.Events;
using Xunit;

namespace WarlordEngine.Tests;

public class Task7BPlaytestingFixesTests
{
	[Fact]
	public void Fix1_ItemEquip_Enforces_Level_Restriction()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "item_sword_lvl2",
			Name = "Heavy Broadsword",
			Type = CardType.Item,
			Level = 2,
			Class = CharacterClass.Fighter
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "fighter_lvl1",
			Name = "Recruit",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Fighter
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "fighter_lvl2",
			Name = "Veteran",
			Type = CardType.Character,
			Level = 2,
			Class = CharacterClass.Fighter
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
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0,
			CurrentSkill = 1
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0,
			CurrentSkill = 2
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
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
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
			CardInstanceId = 1,
			TargetCharacterId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Level 2"));
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			TargetCharacterId = 3
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess);
		List<PlayCardAction> list4 = gameEngine.GetLegalActions(state, 0).OfType<PlayCardAction>().ToList();
		Assert.Single(list4);
		Assert.Equal(3, list4[0].TargetCharacterId);
	}

	[Fact]
	public void Fix1_ItemEquip_Enforces_Class_And_Armor_Restrictions()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "scale_mail",
			Name = "Scale Mail",
			Type = CardType.Item,
			Level = 1,
			Class = CharacterClass.Fighter,
			Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Armor", "Mail" })
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "breastplate_of_power",
			Name = "Breastplate of Power",
			Type = CardType.Item,
			Level = 1,
			Class = CharacterClass.Fighter,
			Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "Armor", "Plate" })
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "rogue_char",
			Name = "Thief",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Rogue
		};
		CardDefinition cardDefinition4 = new CardDefinition
		{
			DefinitionId = "fighter_char",
			Name = "Warrior",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Fighter
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3,
				[cardDefinition4.DefinitionId] = cardDefinition4
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0,
			CurrentSkill = 1
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = cardDefinition4.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0,
			CurrentSkill = 1
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
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
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
		ActionResult actionResult = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			TargetCharacterId = 2
		});
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("does not match Item class"));
		ActionResult actionResult2 = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			TargetCharacterId = 3
		});
		Assert.True(actionResult2.IsSuccess);
		cardInstance3.AttachedItemIds.Add(1);
		gameState2.Players[0].Ranks[1][1] = cardInstance3;
		gameState2.Players[0].DiscardPile.Add(cardInstance with
		{
			Zone = CardZone.Attached,
			AttachedToInstanceId = 3
		});
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 4,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		PlayerState playerState2 = gameState2.Players[0];
		num = 1;
		List<CardInstance> list4 = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list4, num);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance4;
		playerState2.Hand = list4;
		ActionResult actionResult3 = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 4,
			TargetCharacterId = 3
		});
		Assert.False(actionResult3.IsSuccess);
		Assert.Contains(actionResult3.Errors, (string e) => e.Contains("already has armor equipped"));
	}

	[Fact]
	public void Fix2_StrongarmStout_Has_Zero_Passive_Stats()
	{
		CardDefinition itemDef = new CardDefinition
		{
			DefinitionId = "strongarm_stout",
			Name = "Strongarm Stout",
			Type = CardType.Item,
			Class = CharacterClass.Classless,
			RulesText = "Destroy Order: Each player draws a card. This turn, this character gains +2 ATK and +1 HP for each card drawn this way.",
			Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Potion")
		};
		(int, int, int) itemPassiveStats = GameEngine.GetItemPassiveStats(itemDef);
		Assert.Equal(0, itemPassiveStats.Item1);
		Assert.Equal(0, itemPassiveStats.Item2);
		Assert.Equal(0, itemPassiveStats.Item3);
	}

	[Fact]
	public void Fix2_Destroy_Cost_Item_Moves_To_Discard_When_Activated()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "strongarm_stout",
			Name = "Strongarm Stout",
			Type = CardType.Item,
			Class = CharacterClass.Classless,
			RulesText = "Destroy Order: Each player draws a card.",
			Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Potion")
		};
		List<AbilityDefinition> list = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		Assert.NotEmpty(list);
		Assert.True(list[0].DestroysSource);
		CardDefinition cardDefinition2 = cardDefinition with
		{
			Abilities = list
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "dwarf_warrior",
			Name = "Dwarf Warrior",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Fighter
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Attached,
			AttachedToInstanceId = 20,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 20,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0
		};
		int num = 1;
		List<int> list2 = new List<int>(num);
		CollectionsMarshal.SetCount(list2, num);
		CollectionsMarshal.AsSpan(list2)[0] = 10;
		cardInstance2.AttachedItemIds = list2;
		CardInstance cardInstance3 = cardInstance2;
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main
		};
		num = 2;
		List<PlayerState> list3 = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list3, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list3);
		ref PlayerState reference = ref span[0];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance3;
		sortedDictionary[1] = list4;
		playerState.Ranks = sortedDictionary;
		num2 = 1;
		List<CardInstance> list5 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list5, num2);
		CollectionsMarshal.AsSpan(list5)[0] = cardInstance;
		playerState.DiscardPile = list5;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list3;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 10,
			AbilityIndex = 0,
			TargetInstanceIds = Array.Empty<int>()
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.DoesNotContain(10, gameState2.Players[0].Ranks[1][0].AttachedItemIds);
		CardInstance cardInstance4 = gameState2.Players[0].DiscardPile.First((CardInstance c) => c.InstanceId == 10);
		Assert.Equal(CardZone.Discard, cardInstance4.Zone);
		Assert.Null(cardInstance4.AttachedToInstanceId);
	}

	[Fact]
	public void Fix3_AssassinsStrike_Blocks_Ready_Targets_And_Allows_Spent()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "assassins_strike",
			Name = "Assassin's Strike",
			Type = CardType.Action,
			Class = CharacterClass.Rogue,
			Level = 1,
			RulesText = "Order: Perform a melee strike against a spent character. The strike inflicts an additional wound."
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "rogue",
			Name = "Shadowblade",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Rogue
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "enemy_guard",
			Name = "Enemy Guard",
			Type = CardType.Character,
			Level = 1,
			Class = CharacterClass.Fighter,
			ArmorClass = 10,
			BaseHitPoints = 3
		};
		List<AbilityDefinition> abilities = RulesParser.Parse(cardDefinition.RulesText, cardDefinition.Name);
		CardDefinition cardDefinition4 = cardDefinition with
		{
			Abilities = abilities
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition4.DefinitionId] = cardDefinition4,
				[cardDefinition2.DefinitionId] = cardDefinition2,
				[cardDefinition3.DefinitionId] = cardDefinition3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition4.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready,
			CurrentArmorClass = 10,
			CurrentHitPoints = 3
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 4,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Spent,
			CurrentArmorClass = 10,
			CurrentHitPoints = 3
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
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list4);
		span2[0] = cardInstance3;
		span2[1] = cardInstance4;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			PerformerInstanceId = 2,
			TargetCharacterId = 3
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("spent or stunned"));
		PlayCardAction action2 = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			PerformerInstanceId = 2,
			TargetCharacterId = 4
		};
		ActionResult actionResult2 = gameEngine.CanExecute(action2);
		Assert.True(actionResult2.IsSuccess);
		List<PlayCardAction> list5 = (from a in gameEngine.GetLegalActions(state, 0).OfType<PlayCardAction>()
			where a.CardInstanceId == 1
			select a).ToList();
		Assert.Single(list5);
		Assert.Equal(4, list5[0].TargetCharacterId);
	}

	[Fact]
	public void Fix3_AssassinsStrike_Inflicts_Extra_Wound_On_Hit()
	{
		ResolveAttackEffect resolveAttackEffect = new ResolveAttackEffect
		{
			StrikeBonus = 5,
			AdditionalWounds = 1,
			Damage = 1
		};
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "target_dummy",
			Name = "Target Dummy",
			Type = CardType.Character,
			ArmorClass = 10,
			BaseHitPoints = 5
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			CurrentArmorClass = 10,
			CurrentHitPoints = 5
		};
		GameState gameState = new GameState();
		int num = 2;
		List<PlayerState> list = new List<PlayerState>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<PlayerState> span = CollectionsMarshal.AsSpan(list);
		span[0] = new PlayerState
		{
			PlayerIndex = 0
		};
		ref PlayerState reference = ref span[1];
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[1] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		gameState.Players = list;
		GameState state = gameState;
		EffectContext context = new EffectContext
		{
			SourceInstanceId = 1,
			TargetInstanceId = 10,
			State = state,
			CardDb = cardDb,
			RollDice = (int c, int s) => 15
		};
		List<GameEvent> source = resolveAttackEffect.Evaluate(context).ToList();
		DealDamageEvent dealDamageEvent = Assert.Single(source.OfType<DealDamageEvent>());
		Assert.Equal(2, dealDamageEvent.Amount);
	}

	[Fact]
	public void Fix4_Melee_Without_Reach_Requires_Rank1_vs_Rank1()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "normal_warrior",
			Name = "Normal Warrior",
			Type = CardType.Character,
			Level = 1
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[2] = list2;
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
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		DeclareAttackAction action = new DeclareAttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			DefenderInstanceId = 2
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("rank sum <= 2"));
	}

	[Fact]
	public void Fix4_ShamblingHorror_Reach_Allows_Rank2_vs_Rank1()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "shambling_horror",
			Name = "Shambling Horror",
			Type = CardType.Character,
			Level = 2,
			Traits = new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "Reach", "Undead", "Monster" })
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "enemy_soldier",
			Name = "Enemy Soldier",
			Type = CardType.Character,
			Level = 1
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				[cardDefinition.DefinitionId] = cardDefinition,
				[cardDefinition2.DefinitionId] = cardDefinition2
			}
		};
		Assert.True(cardDefinition.HasReach);
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Combat
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[2] = list2;
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
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance2;
		sortedDictionary2[1] = list3;
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance3;
		sortedDictionary2[2] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		DeclareAttackAction action = new DeclareAttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			DefenderInstanceId = 2
		};
		Assert.True(gameEngine.CanExecute(action).IsSuccess);
		DeclareAttackAction action2 = new DeclareAttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			DefenderInstanceId = 3
		};
		ActionResult actionResult = gameEngine.CanExecute(action2);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("rank sum <= 3"));
		List<AttackAction> collection = gameEngine.GetLegalActions(state, 0).OfType<AttackAction>().ToList();
		Assert.Contains(collection, (AttackAction a) => a.TargetInstanceId == 2);
		Assert.DoesNotContain(collection, (AttackAction a) => a.TargetInstanceId == 3);
	}

	[Fact]
	public void Fix5_Starbinder_Parses_Two_Labeled_Modal_Choices()
	{
		string rulesText = "Limited React: After your target character moves: The target gains your choice of +3 ATK or AC this turn.";
		List<AbilityDefinition> list = RulesParser.Parse(rulesText, "Starbinder");
		Assert.Equal(2, list.Count);
		Assert.Contains(list, (AbilityDefinition a) => a.Description.Contains("(+3 ATK)") && a.IsLimited);
		Assert.Contains(list, (AbilityDefinition a) => a.Description.Contains("(+3 AC)") && a.IsLimited);
		AbilityDefinition abilityDefinition = list.First((AbilityDefinition a) => a.Description.Contains("(+3 ATK)"));
		AbilityDefinition abilityDefinition2 = list.First((AbilityDefinition a) => a.Description.Contains("(+3 AC)"));
		ModifyStatEffect modifyStatEffect = Assert.Single(abilityDefinition.Effects.OfType<ModifyStatEffect>());
		Assert.Equal("ATK", modifyStatEffect.StatName);
		Assert.Equal(3, modifyStatEffect.Amount);
		ModifyStatEffect modifyStatEffect2 = Assert.Single(abilityDefinition2.Effects.OfType<ModifyStatEffect>());
		Assert.Equal("CurrentArmorClass", modifyStatEffect2.StatName);
		Assert.Equal(3, modifyStatEffect2.Amount);
	}

	[Fact]
	public void Fix5_Starbinder_Targets_Only_Moved_Character_And_Enforces_Limited()
	{
		string rulesText = "Limited React: After your target character moves: The target gains your choice of +3 ATK or AC this turn.";
		List<AbilityDefinition> abilities = RulesParser.Parse(rulesText, "Starbinder");
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "starbinder",
			Name = "Starbinder",
			Type = CardType.Character,
			Level = 3,
			Abilities = abilities
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "other_char",
			Name = "Other Character",
			Type = CardType.Character,
			Level = 1
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
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
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
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		sortedDictionary[1] = list2;
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary[2] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		gameState2.Priority.IsReactWindowOpen = true;
		gameState2.Priority.ActivePlayerIndex = 0;
		gameState2.PendingEvents.Add(new MarchEvent
		{
			PlayerIndex = 0,
			CardInstanceId = 2,
			SourceRank = 2,
			DestinationRank = 1
		});
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		List<UseAbilityAction> list4 = gameEngine.GetLegalActions(gameState2, 0).OfType<UseAbilityAction>().ToList();
		Assert.Equal(2, list4.Count);
		Assert.All(list4, (UseAbilityAction a) =>
		{
			Assert.Equal(1, a.SourceInstanceId);
			Assert.Single(a.TargetInstanceIds);
			Assert.Equal(2, a.TargetInstanceIds[0]);
		});
		ActionResult actionResult = gameEngine.Execute(list4[0]);
		Assert.True(actionResult.IsSuccess);
		List<UseAbilityAction> collection = gameEngine.GetLegalActions(gameState2, 0).OfType<UseAbilityAction>().ToList();
		Assert.Empty(collection);
	}

	[Fact]
	public void Fix6_AutoPassWhenNoReactsAvailable_Does_Not_Pause_When_Empty()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "simple_warrior",
			Name = "Simple Warrior",
			Type = CardType.Character,
			Level = 1
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance c = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready
		};
		GameState gameState = new GameState
		{
			ActivePlayerIndex = 0,
			CurrentPhase = TurnPhase.Main,
			AutoPassWhenNoReactsAvailable = true
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
		CollectionsMarshal.AsSpan(list2)[0] = p0Warrior(c);
		sortedDictionary[2] = list2;
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
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		MarchAction action = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 1,
			SourceRank = 2,
			DestinationRank = 1
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.False(gameState2.Priority.IsReactWindowOpen);
	}

	[Fact]
	public void Fix7_MarchAction_Formats_With_Card_Name_And_Id()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "tyren_ruskin",
			Name = "Tyren Ruskin",
			Type = CardType.Character,
			Level = 2
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition> { [cardDefinition.DefinitionId] = cardDefinition }
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 2,
			OwnerPlayerIndex = 0
		};
		GameState gameState = new GameState();
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance;
		sortedDictionary[2] = list2;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		gameState.Players = list;
		GameState state = gameState;
		MarchAction action = new MarchAction
		{
			PlayerIndex = 0,
			CardInstanceId = 2,
			SourceRank = 2,
			DestinationRank = 1
		};
		string actual = Program.FormatAction(action, state, cardDb);
		Assert.Equal("March Tyren Ruskin (ID: 2) from Rank 2 to Rank 1", actual);
	}

	[Fact]
	public void Fix8_Hierarchical_Grouping_Groups_Actions_By_Source()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "brine_fiend",
			Name = "Brine Fiend",
			Type = CardType.Character,
			Level = 1
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "enemy",
			Name = "Enemy",
			Type = CardType.Character,
			Level = 1
		};
		CardDefinition cardDefinition3 = new CardDefinition
		{
			DefinitionId = "gapfinder",
			Name = "Gapfinder",
			Type = CardType.Item,
			Level = 1
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
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 4,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = cardDefinition3.DefinitionId,
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 11,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 12,
			DefinitionId = cardDefinition.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance5 = new CardInstance
		{
			InstanceId = 5,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1
		};
		CardInstance cardInstance6 = new CardInstance
		{
			InstanceId = 6,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1
		};
		CardInstance cardInstance7 = new CardInstance
		{
			InstanceId = 7,
			DefinitionId = cardDefinition2.DefinitionId,
			Zone = CardZone.Board,
			Rank = 1,
			OwnerPlayerIndex = 1
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
		CollectionsMarshal.AsSpan(list2)[0] = cardInstance2;
		playerState.Hand = list2;
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		num2 = 3;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance;
		span2[1] = cardInstance3;
		span2[2] = cardInstance4;
		sortedDictionary[1] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 3;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		Span<CardInstance> span3 = CollectionsMarshal.AsSpan(list4);
		span3[0] = cardInstance5;
		span3[1] = cardInstance6;
		span3[2] = cardInstance7;
		sortedDictionary2[1] = list4;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState state = gameState;
		List<IPlayerAction> legalActions = new List<IPlayerAction>
		{
			new AttackAction
			{
				PlayerIndex = 0,
				AttackerInstanceId = 4,
				TargetInstanceId = 5
			},
			new AttackAction
			{
				PlayerIndex = 0,
				AttackerInstanceId = 4,
				TargetInstanceId = 6
			},
			new AttackAction
			{
				PlayerIndex = 0,
				AttackerInstanceId = 4,
				TargetInstanceId = 7
			},
			new PlayCardAction
			{
				PlayerIndex = 0,
				CardInstanceId = 10,
				TargetCharacterId = 11
			},
			new PlayCardAction
			{
				PlayerIndex = 0,
				CardInstanceId = 10,
				TargetCharacterId = 12
			},
			new PassAction
			{
				PlayerIndex = 0
			}
		};
		List<Program.ActionGroup> list5 = Program.GroupLegalActions(legalActions, state, cardDb);
		Assert.Equal(3, list5.Count);
		Program.ActionGroup actionGroup = list5.First((Program.ActionGroup g) => g.DisplayTitle.Contains("Attack with Brine Fiend"));
		Assert.Contains("3 targets", actionGroup.DisplayTitle);
		Assert.Equal(3, actionGroup.Items.Count);
		Program.ActionGroup actionGroup2 = list5.First((Program.ActionGroup g) => g.DisplayTitle.Contains("Gapfinder"));
		Assert.Contains("2 valid characters", actionGroup2.DisplayTitle);
		Assert.Equal(2, actionGroup2.Items.Count);
		Program.ActionGroup actionGroup3 = list5.First((Program.ActionGroup g) => g.DisplayTitle.Contains("Pass"));
		Assert.Single(actionGroup3.Items);
	}

	private static CardInstance p0Warrior(CardInstance c)
	{
		return c;
	}
}
