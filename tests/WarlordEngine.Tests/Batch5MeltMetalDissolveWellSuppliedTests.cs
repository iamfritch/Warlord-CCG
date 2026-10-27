using System;
using System.Collections.Generic;
using System.Linq;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class Batch5MeltMetalDissolveWellSuppliedTests
{
	private readonly CardDatabase _cardDb;

	public Batch5MeltMetalDissolveWellSuppliedTests()
	{
		_cardDb = DatabaseLoader.LoadFromFile(DataPaths.CardsJsonPath);
	}

	private CardDatabase WithDefs(params CardDefinition[] extraDefs)
	{
		Dictionary<string, CardDefinition> dict = new Dictionary<string, CardDefinition>(_cardDb.Definitions, StringComparer.OrdinalIgnoreCase);
		foreach (CardDefinition def in extraDefs)
		{
			dict[def.DefinitionId] = def;
		}
		return new CardDatabase { Definitions = dict };
	}

	private CardDefinition GetDef(string name)
	{
		CardDefinition? def = _cardDb.Definitions.Values.FirstOrDefault((CardDefinition d) => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
		if (def == null)
		{
			string id = name.ToLowerInvariant().Replace(" ", "_").Replace("'", "").Replace(",", "");
			def = _cardDb.GetDefinition(id);
		}
		Assert.NotNull(def);
		return def!;
	}

	private static PlayerState CreatePlayer(int index, string name)
	{
		return new PlayerState
		{
			PlayerIndex = index,
			DisplayName = name,
			IsSimulation = true,
			Hand = new List<CardInstance>(),
			Deck = new List<CardInstance>(),
			DiscardPile = new List<CardInstance>(),
			BanishPile = new List<CardInstance>(),
			AttachedItems = new List<CardInstance>(),
			Ranks = new SortedDictionary<int, List<CardInstance>>
			{
				[1] = new List<CardInstance>(),
				[2] = new List<CardInstance>(),
				[3] = new List<CardInstance>()
			}
		};
	}

	private static GameState CreateOrderState(PlayerState p0, PlayerState p1)
	{
		return new GameState
		{
			Players = new List<PlayerState> { p0, p1 },
			ActivePlayerIndex = 0,
			TurnNumber = 1,
			CurrentPhase = TurnPhase.Order,
			TurnState = new TurnState { CurrentPhase = TurnPhase.Order },
			Priority = new PriorityState { ActivePlayerIndex = 0, IsReactWindowOpen = false },
			IsSimulation = true,
			AutoPassWhenNoReactsAvailable = true
		};
	}

	private static CardInstance MakeChar(CardDefinition def, int id, int owner, int rank)
	{
		return new CardInstance
		{
			InstanceId = id,
			DefinitionId = def.DefinitionId,
			OwnerPlayerIndex = owner,
			Rank = rank,
			Zone = CardZone.Board,
			ReadyState = ReadyState.Ready,
			CurrentHitPoints = def.BaseHitPoints ?? 2,
			CurrentArmorClass = def.ArmorClass ?? 12,
			CurrentSkill = def.Skill ?? def.Level,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
			AttachedItemIds = new List<int>()
		};
	}

	private static CardInstance MakeItem(CardDefinition def, int id, int owner, int? holderId = null, CardZone zone = CardZone.Attached)
	{
		return new CardInstance
		{
			InstanceId = id,
			DefinitionId = def.DefinitionId,
			OwnerPlayerIndex = owner,
			Zone = zone,
			AttachedToInstanceId = holderId,
			ReadyState = ReadyState.Ready,
			CurrentHitPoints = 1,
			CurrentArmorClass = 10,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
			AttachedItemIds = new List<int>()
		};
	}

	[Fact]
	public void MeltMetal_ValidatesTwoRankRange_AndDealsWoundPerFailedSavePerEquippedItem()
	{
		CardDefinition meltMetal = GetDef("Melt Metal");
		CardDefinition wizardDef = new CardDefinition
		{
			DefinitionId = "test_wiz",
			Name = "Test Wizard",
			Type = CardType.Character,
			Class = CharacterClass.Wizard,
			Level = 4,
			Skill = 6,
			BaseHitPoints = 3,
			ArmorClass = 12
		};
		CardDefinition targetDef = new CardDefinition
		{
			DefinitionId = "test_target",
			Name = "Test Target",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 2,
			Skill = 1,
			BaseHitPoints = 4,
			ArmorClass = 14
		};
		CardDefinition swordDef = GetDef("Bardiche");
		CardDatabase db = WithDefs(wizardDef, targetDef);

		PlayerState p0 = CreatePlayer(0, "P0");
		PlayerState p1 = CreatePlayer(1, "P1");

		CardInstance wizard = MakeChar(wizardDef, 10, 0, 2);
		CardInstance closeTarget = MakeChar(targetDef, 20, 1, 1);
		CardInstance farTarget = MakeChar(targetDef, 21, 1, 2);

		CardInstance item1 = MakeItem(swordDef, 30, 1, 20);
		CardInstance item2 = MakeItem(swordDef, 31, 1, 20);
		closeTarget.AttachedItemIds.AddRange(new[] { 30, 31 });
		p1.AttachedItems.Add(item1);
		p1.AttachedItems.Add(item2);

		p0.Ranks[2].Add(wizard);
		p1.Ranks[1].Add(closeTarget);
		p1.Ranks[2].Add(farTarget);

		CardInstance meltCard = new CardInstance
		{
			InstanceId = 90,
			DefinitionId = meltMetal.DefinitionId,
			OwnerPlayerIndex = 0,
			Zone = CardZone.Hand
		};
		p0.Hand.Add(meltCard);

		GameState state = CreateOrderState(p0, p1);
		GameEngine engine = new GameEngine(state, db, (int min, int max) => 3);

		// P0 Rank 2 -> P1 Rank 2 has cross-board distance 2 + 2 - 1 = 3 (> 2): must fail
		PlayCardAction outOfRange = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 90,
			PerformerInstanceId = 10,
			TargetCharacterId = 21
		};
		ActionResult outOfRangeResult = engine.CanExecute(outOfRange);
		Assert.False(outOfRangeResult.IsSuccess);
		Assert.Contains(outOfRangeResult.Errors, (string e) => e.Contains("within two ranks", StringComparison.OrdinalIgnoreCase));

		// P0 Rank 2 -> P1 Rank 1 has cross-board distance 2 + 1 - 1 = 2 (<= 2): valid, 2 equipped items -> 2 failed saves -> 2 wounds
		PlayCardAction inRange = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 90,
			PerformerInstanceId = 10,
			TargetCharacterId = 20
		};
		ActionResult canExec = engine.CanExecute(inRange);
		Assert.True(canExec.IsSuccess, string.Join("; ", canExec.Errors));
		ActionResult exec = engine.Execute(inRange);
		Assert.True(exec.IsSuccess, string.Join("; ", exec.Errors));

		CardInstance updatedTarget = state.Players[1].Ranks[1].Single((CardInstance c) => c.InstanceId == 20);
		Assert.Equal(2, updatedTarget.Wounds);
	}

	[Fact]
	public void Dissolve_ValidatesRangeAndLevelLimit_BeforeDestroyingItem()
	{
		CardDefinition dissolve = GetDef("Dissolve");
		CardDefinition clericDef = new CardDefinition
		{
			DefinitionId = "test_cleric_l2",
			Name = "Level 2 Cleric",
			Type = CardType.Character,
			Class = CharacterClass.Cleric,
			Level = 2,
			Skill = 3,
			BaseHitPoints = 2,
			ArmorClass = 12
		};
		CardDefinition highLevelItemDef = new CardDefinition
		{
			DefinitionId = "artifact_blade_l7",
			Name = "Artifact Blade",
			Type = CardType.Item,
			Class = CharacterClass.Classless,
			Level = 7,
			RulesText = ""
		};
		CardDefinition lowLevelItemDef = new CardDefinition
		{
			DefinitionId = "simple_ring_l6",
			Name = "Simple Ring",
			Type = CardType.Item,
			Class = CharacterClass.Classless,
			Level = 6,
			RulesText = ""
		};
		CardDatabase db = WithDefs(clericDef, highLevelItemDef, lowLevelItemDef);

		PlayerState p0 = CreatePlayer(0, "P0");
		PlayerState p1 = CreatePlayer(1, "P1");

		CardInstance cleric = MakeChar(clericDef, 10, 0, 2);
		CardInstance enemyFront = MakeChar(clericDef, 20, 1, 1);
		CardInstance enemyBack = MakeChar(clericDef, 21, 1, 2);

		CardInstance highItem = MakeItem(highLevelItemDef, 30, 1, 20);
		CardInstance lowItem = MakeItem(lowLevelItemDef, 31, 1, 20);
		CardInstance farItem = MakeItem(lowLevelItemDef, 32, 1, 21);

		enemyFront.AttachedItemIds.Add(30);
		enemyFront.AttachedItemIds.Add(31);
		enemyBack.AttachedItemIds.Add(32);
		p1.AttachedItems.AddRange(new[] { highItem, lowItem, farItem });

		p0.Ranks[2].Add(cleric);
		p1.Ranks[1].Add(enemyFront);
		p1.Ranks[2].Add(enemyBack);

		CardInstance dissolveCard = new CardInstance
		{
			InstanceId = 95,
			DefinitionId = dissolve.DefinitionId,
			OwnerPlayerIndex = 0,
			Zone = CardZone.Hand
		};
		p0.Hand.Add(dissolveCard);

		GameState state = CreateOrderState(p0, p1);
		GameEngine engine = new GameEngine(state, db);

		// 1. Out of range (P0 Rank 2 -> P1 Rank 2 = distance 3 > 2)
		ActionResult farResult = engine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 95,
			PerformerInstanceId = 10,
			TargetCharacterId = 32
		});
		Assert.False(farResult.IsSuccess);
		Assert.Contains(farResult.Errors, (string e) => e.Contains("within 2 ranks", StringComparison.OrdinalIgnoreCase));

		// 2. Item level 7 is NOT lower than performer level (2) + 5 (= 7)
		ActionResult highLevelResult = engine.CanExecute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 95,
			PerformerInstanceId = 10,
			TargetCharacterId = 30
		});
		Assert.False(highLevelResult.IsSuccess);
		Assert.Contains(highLevelResult.Errors, (string e) => e.Contains("lower level", StringComparison.OrdinalIgnoreCase));

		// 3. Item level 6 IS lower than performer level (2) + 5 (= 7) and within 2 ranks (distance 2)
		ActionResult validResult = engine.Execute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 95,
			PerformerInstanceId = 10,
			TargetCharacterId = 31
		});
		Assert.True(validResult.IsSuccess, string.Join("; ", validResult.Errors));
		Assert.DoesNotContain(31, state.Players[1].Ranks[1].Single((CardInstance c) => c.InstanceId == 20).AttachedItemIds);
		Assert.Contains(state.Players[1].DiscardPile, (CardInstance c) => c.InstanceId == 31);
		Assert.Equal(ReadyState.Spent, state.Players[0].Ranks[2].Single((CardInstance c) => c.InstanceId == 10).ReadyState);
	}

	[Fact]
	public void WellSupplied_EquipsOnlyLegallyEquippableItems_AtMostOnePerCharacter_AndReturnsRestToDeck()
	{
		CardDefinition wellSupplied = GetDef("Well Supplied");
		CardDefinition fighterDef = new CardDefinition
		{
			DefinitionId = "ws_fighter_l5",
			Name = "Frontline Fighter",
			Type = CardType.Character,
			Class = CharacterClass.Fighter,
			Level = 5,
			BaseHitPoints = 4,
			ArmorClass = 15
		};
		CardDefinition wizardDef = new CardDefinition
		{
			DefinitionId = "ws_wizard_l1",
			Name = "Apprentice Wizard",
			Type = CardType.Character,
			Class = CharacterClass.Wizard,
			Level = 1,
			BaseHitPoints = 2,
			ArmorClass = 11
		};
		CardDefinition fighterSword = new CardDefinition
		{
			DefinitionId = "ws_fighter_sword",
			Name = "Fighter Greatsword",
			Type = CardType.Item,
			Class = CharacterClass.Fighter,
			Level = 4,
			Traits = new List<string> { "Weapon", "Sword" },
			RulesText = ""
		};
		CardDefinition secondFighterSword = new CardDefinition
		{
			DefinitionId = "ws_fighter_sword_2",
			Name = "Fighter Broadsword",
			Type = CardType.Item,
			Class = CharacterClass.Fighter,
			Level = 3,
			Traits = new List<string> { "Weapon", "Sword" },
			RulesText = ""
		};
		CardDefinition highLevelWizardStaff = new CardDefinition
		{
			DefinitionId = "ws_wizard_staff_l6",
			Name = "Archmage Staff",
			Type = CardType.Item,
			Class = CharacterClass.Wizard,
			Level = 6,
			Traits = new List<string> { "Weapon", "Staff" },
			RulesText = ""
		};
		CardDatabase db = WithDefs(fighterDef, wizardDef, fighterSword, secondFighterSword, highLevelWizardStaff);

		PlayerState p0 = CreatePlayer(0, "P0");
		PlayerState p1 = CreatePlayer(1, "P1");

		CardInstance fighter = MakeChar(fighterDef, 10, 0, 1);
		CardInstance wizard = MakeChar(wizardDef, 11, 0, 1);
		p0.Ranks[1].Add(fighter);
		p0.Ranks[1].Add(wizard);

		// Put 3 items in the deck:
		// - highLevelWizardStaff (Level 6 Wizard: neither Fighter nor Level 1 Rank 1 Wizard can legally equip)
		// - fighterSword (Level 4 Fighter: Fighter can legally equip, Wizard cannot)
		// - secondFighterSword (Level 3 Fighter: Fighter already equipped one item, Wizard cannot equip Fighter item)
		p0.Deck.Add(MakeItem(highLevelWizardStaff, 40, 0, null, CardZone.Deck));
		p0.Deck.Add(MakeItem(fighterSword, 41, 0, null, CardZone.Deck));
		p0.Deck.Add(MakeItem(secondFighterSword, 42, 0, null, CardZone.Deck));

		CardInstance wsCard = new CardInstance
		{
			InstanceId = 99,
			DefinitionId = wellSupplied.DefinitionId,
			OwnerPlayerIndex = 0,
			Zone = CardZone.Hand
		};
		p0.Hand.Add(wsCard);

		GameState state = CreateOrderState(p0, p1);
		GameEngine engine = new GameEngine(state, db);

		ActionResult res = engine.Execute(new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 99,
			PerformerInstanceId = 10
		});
		Assert.True(res.IsSuccess, string.Join("; ", res.Errors));

		CardInstance updatedFighter = state.Players[0].Ranks[1].Single((CardInstance c) => c.InstanceId == 10);
		CardInstance updatedWizard = state.Players[0].Ranks[1].Single((CardInstance c) => c.InstanceId == 11);

		// Fighter equipped exactly 1 legal item (41); Wizard could not legally equip any; 2 unequipped items remain in deck
		Assert.Equal(new[] { 41 }, updatedFighter.AttachedItemIds);
		Assert.Empty(updatedWizard.AttachedItemIds);
		Assert.Equal(2, state.Players[0].Deck.Count);
		Assert.Contains(state.Players[0].Deck, (CardInstance c) => c.InstanceId == 40);
		Assert.Contains(state.Players[0].Deck, (CardInstance c) => c.InstanceId == 42);
	}

	[Fact]
	public void TargetFilters_ValidateAcrossAllSelectedTargets_NotJustFirstTarget()
	{
		CardDefinition sourceDef = new CardDefinition
		{
			DefinitionId = "dwarf_healer_src",
			Name = "Dwarf Healer",
			Type = CardType.Character,
			Level = 3,
			BaseHitPoints = 3,
			Traits = new List<string> { "Dwarf", "Dwarven Forges" },
			Abilities = new List<AbilityDefinition>
			{
				new AbilityDefinition
				{
					Trigger = AbilityTrigger.Order,
					Description = "Order: Target your wounded Dwarven Forges character: Ready the target.",
					Effects = new List<IEffect> { new ChangeReadyStateEffect(ReadyState.Ready) },
					TargetScope = TargetScope.Friendly
				}
			}
		};
		CardDefinition unwoundedDwarfDef = new CardDefinition
		{
			DefinitionId = "dwarf_ally",
			Name = "Dwarf Ally",
			Type = CardType.Character,
			Level = 2,
			BaseHitPoints = 3,
			Traits = new List<string> { "Dwarf", "Dwarven Forges" }
		};
		CardDatabase db = WithDefs(sourceDef, unwoundedDwarfDef);

		PlayerState p0 = CreatePlayer(0, "P0");
		PlayerState p1 = CreatePlayer(1, "P1");

		CardInstance src = MakeChar(sourceDef, 1, 0, 1);
		CardInstance woundedDwarf = MakeChar(unwoundedDwarfDef, 2, 0, 1) with { Wounds = 1, CurrentHitPoints = 2, ReadyState = ReadyState.Spent };
		CardInstance unwoundedDwarf = MakeChar(unwoundedDwarfDef, 3, 0, 1) with { Wounds = 0, CurrentHitPoints = 3, ReadyState = ReadyState.Spent };
		p0.Ranks[1].AddRange(new[] { src, woundedDwarf, unwoundedDwarf });

		GameState state = CreateOrderState(p0, p1);
		GameEngine engine = new GameEngine(state, db);

		// First target (2) is valid (wounded Dwarf), second target (3) is invalid (unwounded Dwarf)
		UseAbilityAction action = new UseAbilityAction
		{
			PlayerIndex = 0,
			SourceInstanceId = 1,
			AbilityIndex = 0,
			TargetInstanceIds = new[] { 2, 3 }
		};
		ActionResult canExec = engine.CanExecute(action);
		Assert.False(canExec.IsSuccess);
		Assert.Contains(canExec.Errors, (string e) => e.Contains("wounded Dwarven", StringComparison.OrdinalIgnoreCase));
	}
}
