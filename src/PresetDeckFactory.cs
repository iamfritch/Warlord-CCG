using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WarlordEngine.Actions;

namespace WarlordEngine;

public static class PresetDeckFactory
{
	public sealed class StarterDeckDto
	{
		[JsonPropertyName("DeckName")]
		public string DeckName { get; set; } = string.Empty;

		[JsonPropertyName("Faction")]
		public string Faction { get; set; } = string.Empty;

		[JsonPropertyName("WarlordName")]
		public string WarlordName { get; set; } = string.Empty;

		[JsonPropertyName("StartingArmy")]
		public List<DeckCardEntryDto> StartingArmy { get; set; } = new List<DeckCardEntryDto>();

		[JsonPropertyName("MainDeck")]
		public List<DeckCardEntryDto> MainDeck { get; set; } = new List<DeckCardEntryDto>();
	}

	public sealed class DeckCardEntryDto
	{
		[JsonPropertyName("CardName")]
		public string CardName { get; set; } = string.Empty;

		[JsonPropertyName("Count")]
		public int Count { get; set; } = 1;
	}

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	public static string? FindStarterDecksJsonPath(string? customPath = null)
	{
		if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
		{
			return Path.GetFullPath(customPath);
		}
		string basePath = AppContext.BaseDirectory;
		string[] array = new string[2]
		{
			DataPaths.StarterDecksJsonPath,
			Path.Combine(basePath, "starter_decks.json")
		};
		foreach (string path in array)
		{
			if (File.Exists(path))
			{
				return Path.GetFullPath(path);
			}
		}
		return null;
	}

	public static IReadOnlyList<Deck> LoadStarterDecks(CardDatabase cardDb, string? jsonFilePath = null)
	{
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		string text = FindStarterDecksJsonPath(jsonFilePath);
		if (text == null || !File.Exists(text))
		{
			throw new FileNotFoundException("Could not locate starter_decks.json file at '" + (jsonFilePath ?? "candidate search locations") + "'.");
		}
		return LoadStarterDecksFromJson(File.ReadAllText(text), cardDb);
	}

	public static IReadOnlyList<Deck> LoadStarterDecksFromJson(string jsonContent, CardDatabase cardDb)
	{
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent, "jsonContent");
		List<StarterDeckDto> list = JsonSerializer.Deserialize<List<StarterDeckDto>>(jsonContent, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize starter decks JSON: result was null.");
		Dictionary<string, CardDefinition> dictionary = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (CardDefinition value in cardDb.Definitions.Values)
		{
			dictionary[value.Name.Trim()] = value;
			dictionary.TryAdd(value.DefinitionId.Trim(), value);
		}
		List<Deck> list2 = new List<Deck>(list.Count);
		foreach (StarterDeckDto item in list)
		{
			list2.Add(BuildDeckFromDto(item, cardDb, dictionary));
		}
		return list2;
	}

	public static Deck BuildDeckFromDto(StarterDeckDto dto, CardDatabase cardDb, IReadOnlyDictionary<string, CardDefinition> nameLookup)
	{
		ArgumentNullException.ThrowIfNull(dto, "dto");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		ArgumentNullException.ThrowIfNull(nameLookup, "nameLookup");
		if (string.IsNullOrWhiteSpace(dto.WarlordName))
		{
			throw new InvalidOperationException("Deck '" + dto.DeckName + "' does not specify a WarlordName.");
		}
		if (!nameLookup.TryGetValue(dto.WarlordName.Trim(), out CardDefinition value))
		{
			throw new KeyNotFoundException($"Warlord '{dto.WarlordName}' specified in starter deck '{dto.DeckName}' was not found in CardDatabase.");
		}
		List<string> list = new List<string>();
		foreach (DeckCardEntryDto item in dto.StartingArmy)
		{
			if (!string.IsNullOrWhiteSpace(item.CardName))
			{
				if (!nameLookup.TryGetValue(item.CardName.Trim(), out CardDefinition value2))
				{
					throw new KeyNotFoundException($"Card '{item.CardName}' specified in starting army of starter deck '{dto.DeckName}' was not found in CardDatabase.");
				}
				int num = ((item.Count <= 0) ? 1 : item.Count);
				for (int i = 0; i < num; i++)
				{
					list.Add(value2.DefinitionId);
				}
			}
		}
		List<string> list2 = new List<string>();
		foreach (DeckCardEntryDto item2 in dto.MainDeck)
		{
			if (!string.IsNullOrWhiteSpace(item2.CardName))
			{
				if (!nameLookup.TryGetValue(item2.CardName.Trim(), out CardDefinition value3))
				{
					throw new KeyNotFoundException($"Card '{item2.CardName}' specified in main deck of starter deck '{dto.DeckName}' was not found in CardDatabase.");
				}
				int num2 = ((item2.Count <= 0) ? 1 : item2.Count);
				for (int j = 0; j < num2; j++)
				{
					list2.Add(value3.DefinitionId);
				}
			}
		}
		Deck deck = new Deck
		{
			DeckName = dto.DeckName,
			Faction = dto.Faction,
			WarlordDefinitionId = value.DefinitionId,
			StartingCharacterDefinitionIds = list,
			CardDefinitionIds = list2
		};
		ActionResult actionResult = deck.Validate(cardDb);
		if (!actionResult.IsSuccess)
		{
			Console.WriteLine("[PresetDeckFactory] Warning: Starter deck '" + deck.DeckName + "' validation warnings: " + string.Join("; ", actionResult.Errors));
		}
		return deck;
	}

	public static IReadOnlyList<Deck> GetSixFactionStarterDecks(CardDatabase cardDb, int targetDeckSize = 50, string? jsonPath = null)
	{
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		string text = FindStarterDecksJsonPath(jsonPath);
		if (text != null && File.Exists(text))
		{
			try
			{
				IReadOnlyList<Deck> readOnlyList = LoadStarterDecks(cardDb, text);
				if (readOnlyList.Count >= 6)
				{
					Console.WriteLine($"[PresetDeckFactory] Successfully loaded {readOnlyList.Count} starter deck(s) from '{text}'.");
					return readOnlyList;
				}
			}
			catch (KeyNotFoundException)
			{
				throw;
			}
			catch (Exception ex2)
			{
				Console.WriteLine("[PresetDeckFactory] Could not load starter_decks.json: " + ex2.Message + ". Falling back to dynamic preset generation...");
			}
		}
		else
		{
			Console.WriteLine("[PresetDeckFactory] starter_decks.json not found on disk. Falling back to dynamic preset generation...");
		}
		string[] array = new string[6] { "Deverenian", "Dwarf", "Elf", "Free Kingdoms", "Mercenary", "Nothrog" };
		IReadOnlyList<Deck> source = GeneratePresetDecks(cardDb, targetDeckSize);
		List<Deck> list = new List<Deck>();
		string[] array2 = array;
		foreach (string faction in array2)
		{
			Deck deck = source.FirstOrDefault((Deck d) =>
			{
				CardDefinition definition = cardDb.GetDefinition(d.WarlordDefinitionId);
				return definition != null && (definition.GetEffectiveFaction().Contains(faction, StringComparison.OrdinalIgnoreCase) || faction.Contains(definition.GetEffectiveFaction(), StringComparison.OrdinalIgnoreCase));
			});
			if (deck != null)
			{
				list.Add(deck);
				continue;
			}
			CardDefinition cardDefinition = cardDb.Definitions.Values.FirstOrDefault((CardDefinition d) => d.IsWarlord && (d.GetEffectiveFaction().Contains(faction, StringComparison.OrdinalIgnoreCase) || faction.Contains(d.GetEffectiveFaction(), StringComparison.OrdinalIgnoreCase)));
			if (cardDefinition != null)
			{
				Deck deck2 = BuildDeckForWarlord(cardDefinition, cardDb, targetDeckSize);
				if (deck2 != null)
				{
					list.Add(deck2);
				}
			}
		}
		return list;
	}

	public static IReadOnlyList<Deck> GeneratePresetDecks(CardDatabase cardDb, int targetDeckSize = 50)
	{
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		List<CardDefinition> list = cardDb.Definitions.Values.Where((CardDefinition d) => d.IsWarlord).ToList();
		Console.WriteLine($"[PresetDeckFactory] Found {list.Count} Warlord(s) with IsWarlord == true in CardDatabase.");
		if (list.Count == 0)
		{
			throw new InvalidOperationException("No valid Warlords found in card database.");
		}
		List<Deck> list2 = new List<Deck>();
		foreach (CardDefinition item in list)
		{
			Deck deck = BuildDeckForWarlord(item, cardDb, targetDeckSize);
			if (deck != null)
			{
				list2.Add(deck);
			}
		}
		if (list2.Count == 0 && targetDeckSize > 10)
		{
			Console.WriteLine("[PresetDeckFactory] Attempting fallback preset generation with reduced deck size for sample...");
			foreach (CardDefinition item2 in list)
			{
				Deck deck2 = BuildDeckForWarlord(item2, cardDb, 10);
				if (deck2 != null)
				{
					list2.Add(deck2);
				}
			}
		}
		if (list2.Count == 0)
		{
			throw new InvalidOperationException("Failed to construct any valid preset decks from card database.");
		}
		Console.WriteLine($"[PresetDeckFactory] Successfully generated {list2.Count} legal preset deck(s).");
		return list2;
	}

	public static Deck? BuildDeckForWarlord(CardDefinition warlord, CardDatabase cardDb, int targetDeckSize = 50)
	{
		ArgumentNullException.ThrowIfNull(warlord, "warlord");
		ArgumentNullException.ThrowIfNull(cardDb, "cardDb");
		if (!warlord.IsWarlord)
		{
			Console.WriteLine($"[PresetDeckFactory] Card '{warlord.Name}' ({warlord.DefinitionId}) is not marked as IsWarlord == true.");
			return null;
		}
		string warlordFaction = warlord.GetEffectiveFaction();
		List<CardDefinition> source = (from d in cardDb.Definitions.Values
			where !d.IsWarlord && d.Type == CardType.Character
			where !d.Traits.Any((string t) => t.Equals("Reserved", StringComparison.OrdinalIgnoreCase)) && !d.RulesText.Contains("Reserved", StringComparison.OrdinalIgnoreCase)
			where d.GetEffectiveFaction().Equals(warlordFaction, StringComparison.OrdinalIgnoreCase)
			select d).ToList();
		List<CardDefinition> list = source.Where((CardDefinition d) => d.Level == 2).ToList();
		List<CardDefinition> list2 = source.Where((CardDefinition d) => d.Level == 1).ToList();
		if (list.Count == 0 || list2.Count == 0)
		{
			Console.WriteLine($"[PresetDeckFactory] Skipping Warlord '{warlord.Name}' ({warlord.DefinitionId}): Insufficient Level 2 ({list.Count}) or Level 1 ({list2.Count}) faction troops.");
			return null;
		}
		List<string> list3 = new List<string>();
		for (int num = 0; num < 2; num++)
		{
			CardDefinition cardDefinition = list[num % list.Count];
			list3.Add(cardDefinition.DefinitionId);
		}
		for (int num2 = 0; num2 < 3; num2++)
		{
			CardDefinition cardDefinition2 = list2[num2 % list2.Count];
			list3.Add(cardDefinition2.DefinitionId);
		}
		List<CardDefinition> list4 = (from d in cardDb.Definitions.Values
			where !d.IsWarlord
			where d.GetEffectiveFaction().Equals(warlordFaction, StringComparison.OrdinalIgnoreCase) || d.GetEffectiveFaction().Equals("Mercenary", StringComparison.OrdinalIgnoreCase) || d.Traits.Contains("Mercenary", StringComparer.OrdinalIgnoreCase) || d.GetEffectiveFaction().Equals("Neutral", StringComparison.OrdinalIgnoreCase)
			where d.IsUsableByAlignment(warlord.Alignment)
			where d.IsUsableByClass(warlord.Class, warlord.Traits)
			select d).ToList();
		if (list4.Count == 0)
		{
			Console.WriteLine("[PresetDeckFactory] Skipping Warlord '" + warlord.Name + "': No usable main deck cards match constraints.");
			return null;
		}
		List<string> list5 = new List<string>();
		Dictionary<CardType, int> dictionary = new Dictionary<CardType, int>();
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		int num3 = (int)Math.Ceiling((double)targetDeckSize * 0.5);
		for (int num4 = 0; num4 < 3; num4++)
		{
			if (list5.Count >= targetDeckSize)
			{
				break;
			}
			foreach (CardDefinition item in list4)
			{
				if (list5.Count >= targetDeckSize)
				{
					break;
				}
				int valueOrDefault = dictionary2.GetValueOrDefault(item.DefinitionId, 0);
				if (valueOrDefault < 3)
				{
					int valueOrDefault2 = dictionary.GetValueOrDefault(item.Type, 0);
					if (valueOrDefault2 < num3)
					{
						list5.Add(item.DefinitionId);
						dictionary2[item.DefinitionId] = valueOrDefault + 1;
						dictionary[item.Type] = valueOrDefault2 + 1;
					}
				}
			}
		}
		if (list5.Count < targetDeckSize)
		{
			Console.WriteLine($"[PresetDeckFactory] Skipping Warlord '{warlord.Name}': Card pool supplied {list5.Count}/{targetDeckSize} cards under copy & type constraints.");
			return null;
		}
		Deck deck = new Deck
		{
			DeckName = warlord.Name + "'s " + warlordFaction + " Army",
			Faction = warlordFaction,
			WarlordDefinitionId = warlord.DefinitionId,
			CardDefinitionIds = list5,
			StartingCharacterDefinitionIds = list3
		};
		ActionResult actionResult = deck.Validate(cardDb, targetDeckSize);
		if (!actionResult.IsSuccess)
		{
			Console.WriteLine("[PresetDeckFactory] Skipping Warlord '" + warlord.Name + "': Deck validation failed: " + string.Join("; ", actionResult.Errors));
			return null;
		}
		Console.WriteLine($"[PresetDeckFactory] Built preset deck '{deck.DeckName}' led by Warlord '{warlord.Name}' ({warlord.DefinitionId}) with 6-character starting army (1 Warlord, 2x Lvl2, 3x Lvl1) and {deck.CardDefinitionIds.Count}-card main deck.");
		return deck;
	}
}
