using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public class TabletopRulesAndMechanicsTests
{
	private static CardDatabase CreateTestDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["test_hero"] = new CardDefinition
				{
					DefinitionId = "test_hero",
					Name = "Test Hero",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5,
					Faction = "Mercenary",
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Mercenary")
				},
				["sword"] = new CardDefinition
				{
					DefinitionId = "sword",
					Name = "Iron Longsword",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Weapon")
				},
				["axe"] = new CardDefinition
				{
					DefinitionId = "axe",
					Name = "Battle Axe",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Weapon")
				},
				["shield1"] = new CardDefinition
				{
					DefinitionId = "shield1",
					Name = "Wooden Shield",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Shield")
				},
				["shield2"] = new CardDefinition
				{
					DefinitionId = "shield2",
					Name = "Tower Shield",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Shield")
				},
				["helm1"] = new CardDefinition
				{
					DefinitionId = "helm1",
					Name = "Iron Helm",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Helm")
				},
				["helm2"] = new CardDefinition
				{
					DefinitionId = "helm2",
					Name = "Horned Helm",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Helm")
				},
				["steed1"] = new CardDefinition
				{
					DefinitionId = "steed1",
					Name = "Warhorse",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Steed")
				},
				["steed2"] = new CardDefinition
				{
					DefinitionId = "steed2",
					Name = "Charger",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Level = 1,
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Steed")
				},
				["lvl2_action"] = new CardDefinition
				{
					DefinitionId = "lvl2_action",
					Name = "Heroic Cleave",
					Type = CardType.Action,
					Class = CharacterClass.Fighter,
					Level = 2,
					Faction = "Mercenary",
					RulesText = "Order: Do something"
				},
				["alrayn"] = new CardDefinition
				{
					DefinitionId = "alrayn",
					Name = "Alrayn Gaunt",
					Type = CardType.Character,
					CharacterType = "Warlord",
					Class = CharacterClass.Fighter,
					Level = 4,
					ArmorClass = 14,
					BaseHitPoints = 3,
					Faction = "Mercenary",
					Traits = new _003C_003Ez__ReadOnlySingleElementList<string>("Mercenary"),
					Attack = new AttackValue
					{
						Strikes = new _003C_003Ez__ReadOnlySingleElementList<StrikeDescriptor>(new StrikeDescriptor
						{
							Modifier = 5,
							IsRanged = false
						})
					},
					RulesText = "React: After Alrayn Gaunt moves: Perform a melee strike.",
					Abilities = RulesParser.Parse("React: After Alrayn Gaunt moves: Perform a melee strike.", "Alrayn Gaunt")
				},
				["target_dummy"] = new CardDefinition
				{
					DefinitionId = "target_dummy",
					Name = "Target Dummy",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Level = 1,
					ArmorClass = 10,
					BaseHitPoints = 5,
					Faction = "Elf"
				}
			}
		};
	}

	[Fact]
	public void ItemEquip_Enforces_Single_Weapon_Shield_Helm_Steed_Slots()
	{
		CardDatabase cardDb = CreateTestDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "test_hero",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "sword",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "axe",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance4 = new CardInstance
		{
			InstanceId = 4,
			DefinitionId = "shield1",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance5 = new CardInstance
		{
			InstanceId = 5,
			DefinitionId = "shield2",
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
			PlayerIndex = 0
		};
		int num2 = 4;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		span2[2] = cardInstance4;
		span2[3] = cardInstance5;
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
		ActionResult actionResult = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 2,
			TargetCharacterId = 1
		});
		Assert.True(actionResult.IsSuccess);
		cardInstance.AttachedItemIds.Add(2);
		gameState2.Players[0].Ranks[1][0] = cardInstance;
		gameState2.Players[0].DiscardPile.Add(cardInstance2 with
		{
			Zone = CardZone.Attached,
			AttachedToInstanceId = 1
		});
		ActionResult actionResult2 = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 3,
			TargetCharacterId = 1
		});
		Assert.False(actionResult2.IsSuccess);
		Assert.Contains(actionResult2.Errors, (string e) => e.Contains("already has a Weapon equipped"));
		ActionResult actionResult3 = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 4,
			TargetCharacterId = 1
		});
		Assert.True(actionResult3.IsSuccess);
		cardInstance.AttachedItemIds.Add(4);
		gameState2.Players[0].Ranks[1][0] = cardInstance;
		gameState2.Players[0].DiscardPile.Add(cardInstance4 with
		{
			Zone = CardZone.Attached,
			AttachedToInstanceId = 1
		});
		ActionResult actionResult4 = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 5,
			TargetCharacterId = 1
		});
		Assert.False(actionResult4.IsSuccess);
		Assert.Contains(actionResult4.Errors, (string e) => e.Contains("already has a Shield equipped"));
	}

	[Fact]
	public void ItemEquip_Enforces_No_Duplicate_Items()
	{
		CardDatabase cardDb = CreateTestDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "test_hero",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "sword",
			Zone = CardZone.Hand,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "sword",
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
			PlayerIndex = 0
		};
		int num2 = 2;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list2);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
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
		cardInstance.AttachedItemIds.Add(2);
		gameState2.Players[0].Ranks[1][0] = cardInstance;
		gameState2.Players[0].DiscardPile.Add(cardInstance2 with
		{
			Zone = CardZone.Attached,
			AttachedToInstanceId = 1
		});
		ActionResult actionResult = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 3,
			TargetCharacterId = 1
		});
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("already has 'Iron Longsword' equipped"));
	}

	[Fact]
	public void ActionCard_Enforces_Performer_Level_Not_Skill()
	{
		CardDatabase cardDb = CreateTestDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "test_hero",
			CurrentSkill = 8,
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "lvl2_action",
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
			PlayerIndex = 0
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
		GameState state = gameState;
		GameEngine gameEngine = new GameEngine(state, cardDb);
		ActionResult actionResult = gameEngine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 2,
			PerformerInstanceId = 1
		});
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("does not meet required Level 2"));
	}

	[Fact]
	public void FormationRules_Enforce_Falling_Forward_When_Rank_Collapses()
	{
		PlayerState playerState = new PlayerState
		{
			PlayerIndex = 0
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num = 1;
		List<CardInstance> list = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "char1",
			Zone = CardZone.Board,
			ReadyState = ReadyState.Ready
		};
		sortedDictionary[1] = list;
		num = 2;
		List<CardInstance> list2 = new List<CardInstance>(num);
		CollectionsMarshal.SetCount(list2, num);
		Span<CardInstance> span = CollectionsMarshal.AsSpan(list2);
		span[0] = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "char2",
			Zone = CardZone.Board,
			ReadyState = ReadyState.Ready
		};
		span[1] = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "char3",
			Zone = CardZone.Board,
			ReadyState = ReadyState.Ready
		};
		sortedDictionary[2] = list2;
		playerState.Ranks = sortedDictionary;
		PlayerState playerState2 = playerState;
		BoardGeometry.ResolveFallingForward(playerState2);
		Assert.Equal(2, playerState2.Ranks[1].Count);
		Assert.Single(playerState2.Ranks[2]);
		Assert.Equal(ReadyState.Spent, playerState2.Ranks[1].First((CardInstance c) => c.InstanceId == 2).ReadyState);
	}

	[Fact]
	public void AlraynGaunt_ReactAfterMove_Enumerates_Opposing_Targets()
	{
		CardDatabase cardDb = CreateTestDb();
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "alrayn",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "target_dummy",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			ReadyState = ReadyState.Ready
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
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		int num2 = 1;
		List<CardInstance> list2 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list2, num2);
		CollectionsMarshal.AsSpan(list2)[0] = new CardInstance
		{
			InstanceId = 10,
			DefinitionId = "test_hero",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0
		};
		sortedDictionary[1] = list2;
		num2 = 1;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		CollectionsMarshal.AsSpan(list3)[0] = cardInstance;
		sortedDictionary[2] = list3;
		playerState.Ranks = sortedDictionary;
		reference = playerState;
		ref PlayerState reference2 = ref span[1];
		PlayerState playerState2 = new PlayerState
		{
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 1;
		List<CardInstance> list4 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list4, num2);
		CollectionsMarshal.AsSpan(list4)[0] = cardInstance2;
		sortedDictionary2[1] = list4;
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
		Assert.True(gameEngine.CanExecute(action).IsSuccess);
		gameEngine.Execute(action);
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(1, gameState2.Priority.ActivePlayerIndex);
		gameEngine.Execute(new PassAction
		{
			PlayerIndex = 1
		});
		Assert.True(gameState2.Priority.IsReactWindowOpen);
		Assert.Equal(0, gameState2.Priority.ActivePlayerIndex);
		List<UseAbilityAction> list5 = gameEngine.GetLegalActions(gameState2, 0).OfType<UseAbilityAction>().ToList();
		Assert.NotEmpty(list5);
		UseAbilityAction useAbilityAction = list5.FirstOrDefault((UseAbilityAction a) => a.SourceInstanceId == 1 && (a.TargetInstanceIds?.Contains(2) ?? false));
		Assert.NotNull(useAbilityAction);
	}

	[Fact]
	public void MultiStrike_Attacks_Specified_Different_Targets()
	{
		CardDefinition value = new CardDefinition
		{
			DefinitionId = "dual_striker",
			Name = "Dual Striker",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 3,
			ArmorClass = 14,
			BaseHitPoints = 5,
			Faction = "Mercenary",
			Attack = new AttackValue
			{
				Strikes = new _003C_003Ez__ReadOnlyArray<StrikeDescriptor>(new StrikeDescriptor[2]
				{
					new StrikeDescriptor
					{
						Modifier = 5,
						IsRanged = false
					},
					new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					}
				})
			}
		};
		CardDefinition value2 = new CardDefinition
		{
			DefinitionId = "defender_a",
			Name = "Defender A",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 5,
			Faction = "Elf"
		};
		CardDefinition value3 = new CardDefinition
		{
			DefinitionId = "defender_b",
			Name = "Defender B",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 5,
			Faction = "Elf"
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["dual_striker"] = value,
				["defender_a"] = value2,
				["defender_b"] = value3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "dual_striker",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready,
			CurrentHitPoints = 5
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "defender_a",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 5,
			CurrentArmorClass = 10
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "defender_b",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 5,
			CurrentArmorClass = 10
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
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
			PlayerIndex = 0,
			WarlordInstanceId = 1
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
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		List<int> list4 = new List<int>();
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 20);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2,
			TargetInstanceIds = new int[2] { 2, 3 }
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		CardInstance cardInstance4 = gameState2.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 2);
		Assert.True(cardInstance4.Wounds > 0 || cardInstance4.CurrentHitPoints < 5);
		CardInstance cardInstance5 = gameState2.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 3);
		Assert.True(cardInstance5.Wounds > 0 || cardInstance5.CurrentHitPoints < 5);
	}

	[Fact]
	public void MultiStrike_Prompts_And_Selects_Target_One_At_A_Time()
	{
		CardDefinition value = new CardDefinition
		{
			DefinitionId = "alrayn_multi",
			Name = "Alrayn Gaunt",
			Type = CardType.Character,
			CharacterType = "Warlord",
			Class = CharacterClass.Fighter,
			Level = 4,
			ArmorClass = 14,
			BaseHitPoints = 5,
			Faction = "Mercenary",
			Attack = new AttackValue
			{
				Strikes = new _003C_003Ez__ReadOnlyArray<StrikeDescriptor>(new StrikeDescriptor[3]
				{
					new StrikeDescriptor
					{
						Modifier = 5,
						IsRanged = false
					},
					new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					},
					new StrikeDescriptor
					{
						Modifier = 1,
						IsRanged = false
					}
				})
			}
		};
		CardDefinition value2 = new CardDefinition
		{
			DefinitionId = "def_weak",
			Name = "Weak Vanguard",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 1,
			Faction = "Dwarf"
		};
		CardDefinition value3 = new CardDefinition
		{
			DefinitionId = "def_strong",
			Name = "Dress",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 5,
			Faction = "Dwarf"
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["alrayn_multi"] = value,
				["def_weak"] = value2,
				["def_strong"] = value3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "alrayn_multi",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready,
			CurrentHitPoints = 5
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "def_weak",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 1,
			CurrentArmorClass = 10
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "def_strong",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 5,
			CurrentArmorClass = 10
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
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
			PlayerIndex = 0,
			WarlordInstanceId = 1
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
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		List<int> callbackStrikes = new List<int>();
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 20);
		gameEngine.SelectStrikeTarget = (StrikeTargetPrompt prompt) =>
		{
			callbackStrikes.Add(prompt.StrikeIndex);
			Assert.DoesNotContain(prompt.LegalTargets, (CardInstance c) => c.InstanceId == 2);
			Assert.Contains(prompt.LegalTargets, (CardInstance c) => c.InstanceId == 3);
			return 3;
		};
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Contains(gameState2.Players[1].DiscardPile, (CardInstance c) => c.InstanceId == 2);
		num = 2;
		List<int> list4 = new List<int>(num);
		CollectionsMarshal.SetCount(list4, num);
		Span<int> span3 = CollectionsMarshal.AsSpan(list4);
		span3[0] = 1;
		span3[1] = 2;
		Assert.Equal(list4, callbackStrikes);
		Assert.Equal(2, gameState2.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 3).Wounds);
	}

	[Fact]
	public void MultiStrike_DoesNot_AutoRetarget_When_Defender_Dies_And_No_Choice_Made()
	{
		CardDefinition value = new CardDefinition
		{
			DefinitionId = "striker_2",
			Name = "Two Striker",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 2,
			ArmorClass = 14,
			BaseHitPoints = 5,
			Faction = "Mercenary",
			Attack = new AttackValue
			{
				Strikes = new _003C_003Ez__ReadOnlyArray<StrikeDescriptor>(new StrikeDescriptor[2]
				{
					new StrikeDescriptor
					{
						Modifier = 5,
						IsRanged = false
					},
					new StrikeDescriptor
					{
						Modifier = 3,
						IsRanged = false
					}
				})
			}
		};
		CardDefinition value2 = new CardDefinition
		{
			DefinitionId = "target_weak",
			Name = "Target Weak",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 1,
			Faction = "Elf"
		};
		CardDefinition value3 = new CardDefinition
		{
			DefinitionId = "target_bystander",
			Name = "Target Bystander",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 1,
			ArmorClass = 10,
			BaseHitPoints = 5,
			Faction = "Elf"
		};
		CardDatabase cardDb = new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["striker_2"] = value,
				["target_weak"] = value2,
				["target_bystander"] = value3
			}
		};
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "striker_2",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 0,
			ReadyState = ReadyState.Ready,
			CurrentHitPoints = 5
		};
		CardInstance cardInstance2 = new CardInstance
		{
			InstanceId = 2,
			DefinitionId = "target_weak",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 1,
			CurrentArmorClass = 10
		};
		CardInstance cardInstance3 = new CardInstance
		{
			InstanceId = 3,
			DefinitionId = "target_bystander",
			Zone = CardZone.Board,
			OwnerPlayerIndex = 1,
			CurrentHitPoints = 5,
			CurrentArmorClass = 10
		};
		GameState gameState = new GameState
		{
			TurnNumber = 1,
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
			PlayerIndex = 0,
			WarlordInstanceId = 1
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
			PlayerIndex = 1
		};
		SortedDictionary<int, List<CardInstance>> sortedDictionary2 = new SortedDictionary<int, List<CardInstance>>();
		num2 = 2;
		List<CardInstance> list3 = new List<CardInstance>(num2);
		CollectionsMarshal.SetCount(list3, num2);
		Span<CardInstance> span2 = CollectionsMarshal.AsSpan(list3);
		span2[0] = cardInstance2;
		span2[1] = cardInstance3;
		sortedDictionary2[1] = list3;
		playerState2.Ranks = sortedDictionary2;
		reference2 = playerState2;
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb, (int count, int sides) => 20);
		AttackAction action = new AttackAction
		{
			PlayerIndex = 0,
			AttackerInstanceId = 1,
			TargetInstanceId = 2
		};
		ActionResult actionResult = gameEngine.Execute(action);
		Assert.True(actionResult.IsSuccess);
		Assert.Contains(gameState2.Players[1].DiscardPile, (CardInstance c) => c.InstanceId == 2);
		CardInstance cardInstance4 = gameState2.Players[1].Ranks[1].First((CardInstance c) => c.InstanceId == 3);
		Assert.Equal(0, cardInstance4.Wounds);
		Assert.Equal(5, cardInstance4.CurrentHitPoints);
	}
}
