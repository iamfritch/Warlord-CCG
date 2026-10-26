using System;
using System.IO;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class DatabaseLoaderTests
{
	[Fact]
	public void LoadFromJson_Deserializes_Character_With_All_Fields()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson("[\n  {\n    \"DefinitionId\": \"krun_the_merciless\",\n    \"Name\": \"Krun the Merciless\",\n    \"Type\": \"Character\",\n    \"Class\": \"Fighter\",\n    \"Alignment\": \"Evil\",\n    \"RulesText\": \"Krun hits hard.\",\n    \"Level\": 5,\n    \"AC\": 18,\n    \"Skill\": 7,\n    \"BaseHp\": 4,\n    \"Strikes\": [\n      { \"Bonus\": 5 },\n      { \"Bonus\": 3 }\n    ],\n    \"Tags\": [\"Orc\", \"Warlord\"]\n  }\n]");
		Assert.Single(cardDatabase.Definitions);
		CardDefinition definition = cardDatabase.GetDefinition("krun_the_merciless");
		Assert.NotNull(definition);
		Assert.Equal("Krun the Merciless", definition.Name);
		Assert.Equal(CardType.Character, definition.Type);
		Assert.Equal(CharacterClass.Fighter, definition.Class);
		Assert.Equal(Alignment.Evil, definition.Alignment);
		Assert.Equal(5, definition.Level);
		Assert.Equal(18, definition.ArmorClass);
		Assert.Equal(7, definition.Skill);
		Assert.Equal(4, definition.BaseHitPoints);
		Assert.Equal("Krun hits hard.", definition.RulesText);
		Assert.NotNull(definition.Attack);
		Assert.Equal(2, definition.Attack.Strikes.Count);
		Assert.Equal(5, definition.Attack.Strikes[0].Modifier);
		Assert.Equal(3, definition.Attack.Strikes[1].Modifier);
		Assert.Equal("+5/+3", definition.Attack.RawText);
		Assert.Contains("Orc", definition.Traits);
		Assert.Contains("Warlord", definition.Traits);
	}

	[Fact]
	public void LoadFromJson_Handles_Item_With_No_Combat_Stats()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson("[\n  {\n    \"DefinitionId\": \"magic_sword\",\n    \"Name\": \"Magic Sword\",\n    \"Type\": \"Item\",\n    \"Class\": \"Classless\",\n    \"Alignment\": \"\",\n    \"RulesText\": \"Equip to a fighter.\",\n    \"Tags\": []\n  }\n]");
		CardDefinition definition = cardDatabase.GetDefinition("magic_sword");
		Assert.NotNull(definition);
		Assert.Equal(CardType.Item, definition.Type);
		Assert.Equal(CharacterClass.Classless, definition.Class);
		Assert.Null(definition.Alignment);
		Assert.Null(definition.ArmorClass);
		Assert.Null(definition.Skill);
		Assert.Null(definition.BaseHitPoints);
		Assert.Null(definition.Attack);
	}

	[Fact]
	public void LoadFromJson_Handles_MultiClass_String()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson("[\n  {\n    \"DefinitionId\": \"dual_class_hero\",\n    \"Name\": \"Dual Class Hero\",\n    \"Type\": \"Character\",\n    \"Class\": \"Cleric/Fighter\",\n    \"Alignment\": \"Good\",\n    \"Level\": 3,\n    \"AC\": 14,\n    \"Skill\": 6,\n    \"BaseHp\": 3,\n    \"Strikes\": [{ \"Bonus\": 4 }],\n    \"Tags\": [\"Human\"]\n  }\n]");
		CardDefinition definition = cardDatabase.GetDefinition("dual_class_hero");
		Assert.NotNull(definition);
		Assert.Equal(CharacterClass.Cleric, definition.Class);
		Assert.Contains("MultiClass:Cleric", definition.Traits);
		Assert.Contains("MultiClass:Fighter", definition.Traits);
		Assert.Contains("Human", definition.Traits);
	}

	[Fact]
	public void LoadFromJson_Handles_Token_CardType()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson("[\n  {\n    \"DefinitionId\": \"skeleton_token\",\n    \"Name\": \"Skeleton Token\",\n    \"Type\": \"Token\",\n    \"Class\": \"Classless\",\n    \"Alignment\": \"Evil\",\n    \"Level\": 1,\n    \"AC\": 8,\n    \"Skill\": 0,\n    \"BaseHp\": 1,\n    \"Strikes\": [{ \"Bonus\": 1 }],\n    \"Tags\": [\"Undead\"]\n  }\n]");
		CardDefinition definition = cardDatabase.GetDefinition("skeleton_token");
		Assert.NotNull(definition);
		Assert.Equal(CardType.Token, definition.Type);
	}

	[Fact]
	public void LoadFromJson_Handles_Empty_Alignment_As_Null()
	{
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson("[\n  {\n    \"DefinitionId\": \"neutral_item\",\n    \"Name\": \"Neutral Item\",\n    \"Type\": \"Item\",\n    \"Class\": \"Classless\",\n    \"Alignment\": \"\",\n    \"Tags\": []\n  }\n]");
		CardDefinition definition = cardDatabase.GetDefinition("neutral_item");
		Assert.NotNull(definition);
		Assert.Null(definition.Alignment);
	}

	[Fact]
	public void LoadFromFile_Reads_Real_CardsJson()
	{
		string text = FindCardsJsonPath();
		if (File.Exists(text))
		{
			CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(text);
			Assert.True(cardDatabase.Definitions.Count > 300, $"Expected 350+ definitions, got {cardDatabase.Definitions.Count}");
			CardDefinition definition = cardDatabase.GetDefinition("abd_al-rahiim");
			Assert.NotNull(definition);
			Assert.Equal("Abd al-Rahiim", definition.Name);
			Assert.Equal(CardType.Character, definition.Type);
			Assert.Equal(CharacterClass.Wizard, definition.Class);
			Assert.Equal(Alignment.Evil, definition.Alignment);
			Assert.Equal(3, definition.Level);
			Assert.Equal(9, definition.ArmorClass);
			Assert.Equal(4, definition.Skill);
			Assert.Equal(2, definition.BaseHitPoints);
			Assert.NotNull(definition.Attack);
			Assert.Single(definition.Attack.Strikes);
			Assert.Equal(1, definition.Attack.Strikes[0].Modifier);
			CardDefinition definition2 = cardDatabase.GetDefinition("adathyn_celestial_messenger");
			Assert.NotNull(definition2);
			Assert.Equal(2, definition2.Attack.Strikes.Count);
			Assert.Equal(4, definition2.Attack.Strikes[0].Modifier);
			Assert.Equal(3, definition2.Attack.Strikes[1].Modifier);
			CardDefinition definition3 = cardDatabase.GetDefinition("ablung's_prisms");
			Assert.NotNull(definition3);
			Assert.Equal(CardType.Item, definition3.Type);
			Assert.Null(definition3.ArmorClass);
			Assert.Null(definition3.BaseHitPoints);
		}
	}

	[Fact]
	public void LoadFromFile_Throws_FileNotFoundException_For_Missing_File()
	{
		string basePath = AppContext.BaseDirectory;
		string missingPath = Path.Combine(basePath, "nonexistent", "path", "cards.json");
		Assert.Throws<FileNotFoundException>(() => DatabaseLoader.LoadFromFile(missingPath));
	}

	private static string FindCardsJsonPath()
	{
		return DataPaths.CardsJsonPath;
	}
}
