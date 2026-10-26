using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarlordEngine.Actions;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class PresetDeckFactoryTests
{
	private static string GetRealCardsJsonPath()
	{
		string path = DataPaths.CardsJsonPath;
		if (File.Exists(path))
		{
			return path;
		}
		throw new FileNotFoundException("cards.json not found.", path);
	}

	[Fact]
	public void DatabaseLoader_Strict_Warlord_Identification_Eliminates_False_Positives()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		CardDefinition definition = cardDatabase.GetDefinition("beacon");
		CardDefinition definition2 = cardDatabase.GetDefinition("coward");
		if (definition != null)
		{
			Assert.False(definition.IsWarlord, "Beacon must not be identified as a Warlord.");
		}
		if (definition2 != null)
		{
			Assert.False(definition2.IsWarlord, "Coward must not be identified as a Warlord.");
		}
	}

	[Fact]
	public void GeneratePresetDecks_Detects_All_Faction_Warlords_And_Builds_Valid_Decks()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		IReadOnlyList<Deck> readOnlyList = PresetDeckFactory.GeneratePresetDecks(cardDb);
		Assert.NotEmpty(readOnlyList);
		Assert.True(readOnlyList.Count >= 4, $"Expected at least 4 preset decks generated across factions, found {readOnlyList.Count}.");
		foreach (Deck item in readOnlyList)
		{
			ActionResult actionResult = item.Validate(cardDb);
			Assert.True(actionResult.IsSuccess, "Deck '" + item.DeckName + "' failed validation: " + string.Join("; ", actionResult.Errors));
			Assert.Equal(50, item.CardDefinitionIds.Count);
			Assert.Equal(5, item.StartingCharacterDefinitionIds.Count);
			Dictionary<string, int> dictionary = (from id in item.CardDefinitionIds
				group id by id).ToDictionary((IGrouping<string, string> g) => g.Key, (IGrouping<string, string> g) => g.Count());
			Assert.All(dictionary.Values, (int count) =>
			{
				Assert.True(count <= 3, $"Card copy count {count} exceeds limit 3");
			});
		}
	}

	[Fact]
	public void GetSixFactionStarterDecks_Returns_Six_Core_Faction_Decks()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		IReadOnlyList<Deck> sixFactionStarterDecks = PresetDeckFactory.GetSixFactionStarterDecks(cardDatabase);
		Assert.Equal(6, sixFactionStarterDecks.Count);
		string[] array = new string[6] { "Deverenian", "Dwarf", "Elf", "Free Kingdoms", "Mercenary", "Nothrog" };
		for (int i = 0; i < 6; i++)
		{
			Deck deck = sixFactionStarterDecks[i];
			CardDefinition definition = cardDatabase.GetDefinition(deck.WarlordDefinitionId);
			Assert.NotNull(definition);
			Assert.Contains(array[i], definition.GetEffectiveFaction(), StringComparison.OrdinalIgnoreCase);
			Assert.Equal(50, deck.TotalCardCount);
			Assert.Equal(5, deck.StartingCharacterDefinitionIds.Count);
			Assert.Equal(44, deck.CardDefinitionIds.Count);
			ActionResult actionResult = deck.Validate(cardDatabase);
			Assert.True(actionResult.IsSuccess, string.Join("; ", actionResult.Errors));
		}
	}

	[Fact]
	public void LoadStarterDecks_Throws_Descriptive_Exception_When_CardName_Not_Found()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		string invalidDeckJson = "[\n  {\n    \"DeckName\": \"Invalid Deck\",\n    \"Faction\": \"Deverenian\",\n    \"WarlordName\": \"NonExistentWarlord12345\",\n    \"StartingArmy\": [\n      { \"CardName\": \"Church Inquisitor\", \"Count\": 1 }\n    ],\n    \"MainDeck\": [\n      { \"CardName\": \"Cleave\", \"Count\": 3 }\n    ]\n  }\n]";
		KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(() => PresetDeckFactory.LoadStarterDecksFromJson(invalidDeckJson, cardDb));
		Assert.Contains("NonExistentWarlord12345", ex.Message);
	}

	[Fact]
	public void LoadStarterDecks_Throws_Descriptive_Exception_When_MainDeck_CardName_Not_Found()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDb = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		string invalidDeckJson = "[\n  {\n    \"DeckName\": \"Invalid Main Deck Card\",\n    \"Faction\": \"Deverenian\",\n    \"WarlordName\": \"Church Enforcer Tancred\",\n    \"StartingArmy\": [\n      { \"CardName\": \"Church Inquisitor\", \"Count\": 1 }\n    ],\n    \"MainDeck\": [\n      { \"CardName\": \"ImaginarySwordOfDoomXYZ\", \"Count\": 3 }\n    ]\n  }\n]";
		KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(() => PresetDeckFactory.LoadStarterDecksFromJson(invalidDeckJson, cardDb));
		Assert.Contains("ImaginarySwordOfDoomXYZ", ex.Message);
	}
}
