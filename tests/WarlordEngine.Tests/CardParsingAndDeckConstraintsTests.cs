using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class CardParsingAndDeckConstraintsTests
{
	private static CardDatabase CreateTestCardDb()
	{
		return new CardDatabase
		{
			Definitions = new Dictionary<string, CardDefinition>
			{
				["warlord-fighter"] = new CardDefinition
				{
					DefinitionId = "warlord-fighter",
					Name = "Commander Warlord",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 3,
					ArmorClass = 15,
					BaseHitPoints = 12,
					IsWarlord = true,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Warlord", "Free Kingdoms" }
				},
				["level4-hero-non-warlord"] = new CardDefinition
				{
					DefinitionId = "level4-hero-non-warlord",
					Name = "Veteran Champion",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 4,
					ArmorClass = 16,
					BaseHitPoints = 14,
					IsWarlord = false,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Fighter", "Free Kingdoms" }
				},
				["wizard-only-spell"] = new CardDefinition
				{
					DefinitionId = "wizard-only-spell",
					Name = "Fireball Scroll",
					Type = CardType.Action,
					Class = CharacterClass.Wizard,
					Alignment = Alignment.Good,
					Level = 2,
					ArmorClass = null,
					BaseHitPoints = null,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Spell" }
				},
				["evil-only-item"] = new CardDefinition
				{
					DefinitionId = "evil-only-item",
					Name = "Unholy Blade",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Evil,
					Level = 2,
					ArmorClass = null,
					BaseHitPoints = null,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Weapon" }
				},
				["fighter-sword"] = new CardDefinition
				{
					DefinitionId = "fighter-sword",
					Name = "Iron Broadsword",
					Type = CardType.Item,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 1,
					ArmorClass = null,
					BaseHitPoints = null,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Weapon" }
				},
				["fk-troop-lvl2"] = new CardDefinition
				{
					DefinitionId = "fk-troop-lvl2",
					Name = "Free Kingdoms Veteran",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 2,
					ArmorClass = 14,
					BaseHitPoints = 8,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Free Kingdoms" }
				},
				["fk-troop-lvl1"] = new CardDefinition
				{
					DefinitionId = "fk-troop-lvl1",
					Name = "Free Kingdoms Archer",
					Type = CardType.Character,
					Class = CharacterClass.Fighter,
					Alignment = Alignment.Good,
					Level = 1,
					ArmorClass = 12,
					BaseHitPoints = 6,
					Faction = "Free Kingdoms",
					Traits = new List<string> { "Free Kingdoms" }
				}
			}
		};
	}

	[Fact]
	public void DatabaseLoader_Maps_Card_Type_Properties_Accurately()
	{
		string json = "[\n            {\n                \"DefinitionId\": \"item-sword\",\n                \"Name\": \"Longsword\",\n                \"Type\": \"Item\",\n                \"Class\": \"Fighter\",\n                \"Alignment\": \"Good\",\n                \"Level\": 1,\n                \"AC\": 15,\n                \"BaseHp\": 12,\n                \"Tags\": [\"Weapon\"]\n            }\n        ]";
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson(json);
		CardDefinition definition = cardDatabase.GetDefinition("item-sword");
		Assert.NotNull(definition);
		Assert.Equal(CardType.Item, definition.Type);
		Assert.Equal(CharacterClass.Fighter, definition.Class);
		Assert.Equal(15, definition.ArmorClass);
		Assert.Equal(12, definition.BaseHitPoints);
		Assert.Null(definition.Skill);
		Assert.Null(definition.Attack);
	}

	[Fact]
	public void Strict_Warlord_Identification_Excludes_Level3_Regular_Troops()
	{
		string json = "[\n            {\n                \"DefinitionId\": \"level-3-vet\",\n                \"Name\": \"Veteran Guard\",\n                \"Type\": \"Character\",\n                \"Class\": \"Fighter\",\n                \"Level\": 3,\n                \"Tags\": [\"Guard\"]\n            },\n            {\n                \"DefinitionId\": \"actual-warlord\",\n                \"Name\": \"General Warlord\",\n                \"Type\": \"Character\",\n                \"Class\": \"Fighter\",\n                \"Level\": 5,\n                \"Tags\": [\"Warlord\"]\n            }\n        ]";
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson(json);
		Assert.False(cardDatabase.GetDefinition("level-3-vet")?.IsWarlord);
		Assert.True(cardDatabase.GetDefinition("actual-warlord")?.IsWarlord);
	}

	[Fact]
	public void PresetDeckFactory_Excludes_Cards_With_NonMatching_Class_Or_Alignment()
	{
		CardDatabase cardDatabase = CreateTestCardDb();
		CardDefinition definition = cardDatabase.GetDefinition("warlord-fighter");
		Deck deck = PresetDeckFactory.BuildDeckForWarlord(definition, cardDatabase, 3);
		Assert.NotNull(deck);
		Assert.DoesNotContain("wizard-only-spell", deck.CardDefinitionIds);
		Assert.DoesNotContain("evil-only-item", deck.CardDefinitionIds);
	}

	[Fact]
	public void PresetDeckFactory_Bundles_Valid_Starting_Army_Composition()
	{
		CardDatabase cardDb = CreateTestCardDb();
		CardDefinition definition = cardDb.GetDefinition("warlord-fighter");
		Deck deck = PresetDeckFactory.BuildDeckForWarlord(definition, cardDb, 3);
		Assert.NotNull(deck);
		Assert.NotEmpty(deck.StartingCharacterDefinitionIds);
		Assert.Equal(5, deck.StartingCharacterDefinitionIds.Count);
		int actual = deck.StartingCharacterDefinitionIds.Select((string id) => cardDb.GetDefinition(id)).Count((CardDefinition d) => d.Level == 2);
		int actual2 = deck.StartingCharacterDefinitionIds.Select((string id) => cardDb.GetDefinition(id)).Count((CardDefinition d) => d.Level == 1);
		Assert.Equal(2, actual);
		Assert.Equal(3, actual2);
	}
}
