using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using WarlordEngine.Actions;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class SkillRatingParsingAndValidationTests
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
	public void DatabaseLoader_Cleans_Unicode_Artifacts_And_Parses_Skill_Ratings_And_Adds_Base_Tags()
	{
		string json = "[\n  {\n    \"DefinitionId\": \"archer_ranger\",\n    \"Name\": \"Ranger of the Wild\",\n    \"Type\": \"Character\",\n    \"Class\": \"Fighter\",\n    \"Alignment\": \"Good\",\n    \"Level\": 2,\n    \"AC\": 12,\n    \"BaseHp\": 2,\n    \"RulesText\": \"Marksmanship \\\\u002B5, Charisma: 3, Stealth \\\\u002B2\",\n    \"Tags\": [ \"Elf\", \"Wildlands\\\\u2013Scout\", \"Scribe \\\\u002B4\" ]\n  }\n]";
		CardDatabase cardDatabase = DatabaseLoader.LoadFromJson(json);
		CardDefinition definition = cardDatabase.GetDefinition("archer_ranger");
		Assert.NotNull(definition);
		Assert.Equal("Wildlands-Scout", definition.Traits[1]);
		Assert.Equal("Marksmanship +5, Charisma: 3, Stealth +2", definition.RulesText);
		Assert.Equal(5, definition.SkillRatings["Marksmanship"]);
		Assert.Equal(3, definition.SkillRatings["Charisma"]);
		Assert.Equal(2, definition.SkillRatings["Stealth"]);
		Assert.Equal(4, definition.SkillRatings["Scribe"]);
		Assert.Contains("Marksmanship", definition.Traits);
		Assert.Contains("Charisma", definition.Traits);
		Assert.Contains("Stealth", definition.Traits);
		Assert.Contains("Scribe", definition.Traits);
	}

	[Fact]
	public void DatabaseLoader_Identifies_All_31_Warlords_In_CardsJson()
	{
		string realCardsJsonPath = GetRealCardsJsonPath();
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(realCardsJsonPath);
		string[] array = new string[31]
		{
			"Adathan, Celestial Messenger", "Alrayn Gaunt", "Anacaona", "Annetta Yscar", "Archivist Myrina", "Church Enforcer Tancred", "Commander Retsov", "Efrim Dawntreader", "Gorthos, Shadow's Axe", "Grand Inventor Otto",
			"Grovewarden Elumera", "High Acolyte Hellene", "Hunter Merril", "Kazkah", "Kurak Stonejaw", "Librarian Shastra", "Lord Barrow", "Lord Belasco", "Mair Haven", "Memoriam",
			"Nisha", "Ramesh, Elder Sage", "Rone of the Deepbrook", "Seeker", "Shyessa the Weaver", "Sklot the Cruel", "Tazian Dythanus", "Vactus, Spirit's Champion", "Wa'San Wolf-Spirit", "Warboss Azh'kal",
			"Z'adok, Prey Herder"
		};
		Dictionary<string, CardDefinition> dictionary = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (CardDefinition value2 in cardDatabase.Definitions.Values)
		{
			dictionary[value2.Name] = value2;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			Assert.True(dictionary.TryGetValue(text, out var value), "Warlord '" + text + "' was not found in CardDatabase.");
			Assert.True(value.IsWarlord, "Warlord '" + text + "' should have IsWarlord == true.");
		}
	}

	[Fact]
	public void PlayCardAction_Blocks_Item_Attachment_When_Character_Lacks_Required_Skill_Rating()
	{
		CardDefinition cardToPlay = new CardDefinition
		{
			DefinitionId = "elven_longbow",
			Name = "Elven Longbow",
			Type = CardType.Item,
			Level = 2,
			RulesText = "Marksmanship >= 3\nOrder: Perform a +5 ranged strike."
		};
		CardDefinition targetCharacterDef = new CardDefinition
		{
			DefinitionId = "novice_recruit",
			Name = "Novice Recruit",
			Type = CardType.Character,
			Level = 1,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Marksmanship"] = 2 }
		};
		PlayCardAction playCardAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetCharacterId = 20
		};
		ActionResult actionResult = playCardAction.Validate(cardToPlay, targetCharacterDef);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Marksmanship") && e.Contains("3"));
	}

	[Fact]
	public void PlayCardAction_Allows_Item_Attachment_When_Character_Meets_Required_Skill_Rating()
	{
		CardDefinition cardToPlay = new CardDefinition
		{
			DefinitionId = "elven_longbow",
			Name = "Elven Longbow",
			Type = CardType.Item,
			Level = 2,
			RulesText = "Marksmanship >= 3\nOrder: Perform a +5 ranged strike."
		};
		CardDefinition targetCharacterDef = new CardDefinition
		{
			DefinitionId = "master_archer",
			Name = "Master Archer",
			Type = CardType.Character,
			Level = 2,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Marksmanship"] = 5 }
		};
		PlayCardAction playCardAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetCharacterId = 20
		};
		ActionResult actionResult = playCardAction.Validate(cardToPlay, targetCharacterDef);
		Assert.True(actionResult.IsSuccess);
	}

	[Fact]
	public void PlayCardAction_Blocks_Action_Card_When_Target_Lacks_Required_Skill_Rating()
	{
		CardDefinition cardToPlay = new CardDefinition
		{
			DefinitionId = "arcane_blast",
			Name = "Arcane Blast",
			Type = CardType.Action,
			Level = 1,
			RulesText = "Requires Magic >= 2\nOrder: Deal 3 damage."
		};
		CardDefinition targetCharacterDef = new CardDefinition
		{
			DefinitionId = "fighter_grunt",
			Name = "Fighter Grunt",
			Type = CardType.Character,
			Level = 1,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Marksmanship"] = 3 }
		};
		PlayCardAction playCardAction = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = 10,
			TargetCharacterId = 20
		};
		ActionResult actionResult = playCardAction.Validate(cardToPlay, targetCharacterDef);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Magic") && e.Contains("2"));
	}

	[Fact]
	public void GameEngine_CanExecute_PlayCardAction_Enforces_Skill_Rating_Requirements_On_Target()
	{
		CardDefinition cardDefinition = new CardDefinition
		{
			DefinitionId = "sniper_crossbow",
			Name = "Sniper Crossbow",
			Type = CardType.Item,
			Level = 2,
			RulesText = "Marksmanship >= 4\nOrder: Perform a +6 ranged strike."
		};
		CardDefinition cardDefinition2 = new CardDefinition
		{
			DefinitionId = "novice_hunter",
			Name = "Novice Hunter",
			Type = CardType.Character,
			Level = 2,
			SkillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Marksmanship"] = 1 }
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
			SkillRatings = new Dictionary<string, int>(cardDefinition2.SkillRatings, StringComparer.OrdinalIgnoreCase)
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
		span[1] = new PlayerState
		{
			PlayerIndex = 1
		};
		gameState.Players = list;
		GameState gameState2 = gameState;
		GameEngine gameEngine = new GameEngine(gameState2, cardDb);
		PlayCardAction action = new PlayCardAction
		{
			PlayerIndex = 0,
			CardInstanceId = cardInstance.InstanceId,
			TargetCharacterId = cardInstance2.InstanceId
		};
		ActionResult actionResult = gameEngine.CanExecute(action);
		Assert.False(actionResult.IsSuccess);
		Assert.Contains(actionResult.Errors, (string e) => e.Contains("Marksmanship") && e.Contains("4"));
		cardInstance2.SkillRatings["Marksmanship"] = 4;
		gameState2.Players[0].Ranks[1][0] = cardInstance2;
		ActionResult actionResult2 = gameEngine.CanExecute(action);
		Assert.True(actionResult2.IsSuccess);
	}
}
