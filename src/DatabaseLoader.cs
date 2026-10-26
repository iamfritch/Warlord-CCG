using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WarlordEngine.Actions;
using WarlordEngine.Engine;

namespace WarlordEngine;

public static class DatabaseLoader
{
	internal sealed class CardDto
	{
		[JsonPropertyName("DefinitionId")]
		public string DefinitionId { get; set; } = string.Empty;

		[JsonPropertyName("Name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("Type")]
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public CardType Type { get; set; }

		[JsonPropertyName("Class")]
		public string? Class { get; set; }

		[JsonPropertyName("Alignment")]
		public string? Alignment { get; set; }

		[JsonPropertyName("RulesText")]
		public string? RulesText { get; set; }

		[JsonPropertyName("Level")]
		public int Level { get; set; }

		[JsonPropertyName("AC")]
		public int? AC { get; set; }

		[JsonPropertyName("Skill")]
		public int? Skill { get; set; }

		[JsonPropertyName("BaseHp")]
		public int? BaseHp { get; set; }

		[JsonPropertyName("Strikes")]
		public List<StrikeDto>? Strikes { get; set; }

		[JsonPropertyName("Tags")]
		public List<string>? Tags { get; set; }

		[JsonPropertyName("CharacterType")]
		public string? CharacterType { get; set; }

		[JsonPropertyName("IsWarlord")]
		public bool? IsWarlord { get; set; }

		[JsonPropertyName("Subtype")]
		public string? Subtype { get; set; }
	}

	internal sealed class StrikeDto
	{
		[JsonPropertyName("Bonus")]
		public int Bonus { get; set; }

		[JsonPropertyName("IsRanged")]
		public bool IsRanged { get; set; }
	}

	private static readonly Regex SkillRatingRegex = new Regex("\\b([A-Za-z]+(?:\\s+[A-Za-z]+)*?)\\s*(?::|\\+)\\s*([+-]?\\d+)\\b", RegexOptions.Compiled);

	private static readonly HashSet<string> ExcludedSkillWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"order", "react", "spend order", "spend react", "limited order", "limited react", "death react", "enter react", "roll order", "gain",
		"gains", "perform", "performs", "strike", "strikes", "inflict", "inflicts", "deal", "deals", "damage",
		"wound", "wounds", "within", "draw", "draws", "heal", "heals", "discard", "discards", "move",
		"moves", "adjacent", "target", "targets", "other", "another", "character", "characters", "fighter", "fighters",
		"cleric", "clerics", "wizard", "wizards", "rogue", "rogues", "die", "roll", "base", "dc",
		"rank", "ranks", "level", "levels", "spend", "turn", "each", "all", "your", "opposing",
		"and", "or", "hp", "ac", "atk", "skill", "n", "it", "he", "she",
		"they"
	};

	private static readonly HashSet<string> KnownWarlordDefinitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"adathan,_celestial_messenger", "adathyn_celestial_messenger", "alrayn_gaunt", "anacaona", "annetta_yscar", "archivist_myrina", "church_enforcer_tancred", "commander_retsov", "efrim_dawntreader", "gorthos,_shadow's_axe",
		"grand_inventor_otto", "grovewarden_elumera", "high_acolyte_hellene", "hunter_merril", "kazkah", "kurak_stonejaw", "librarian_shastra", "lord_barrow", "lord_belasco", "mair_haven",
		"memoriam", "memorium", "nisha", "ramesh,_elder_sage", "rone_of_the_deepbrook", "seeker", "shyessa_the_weaver", "sklot_the_cruel", "tazian_dythanus", "taizan_dythanus",
		"vactus,_spirit's_champion", "vactus_spirit's_champion", "warboss_azh'kal", "wa'san_wolf-spirit", "z'adok,_prey_herder"
	};

	private static readonly JsonSerializerOptions DtoSerializerOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		Converters = 
		{
			(JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			(JsonConverter)new JsonStringEnumConverter()
		}
	};

	public static CardDatabase LoadFromFile(string jsonFilePath)
	{
		if (!File.Exists(jsonFilePath))
		{
			throw new FileNotFoundException("Card data file not found: " + jsonFilePath);
		}
		return LoadFromJson(File.ReadAllText(jsonFilePath));
	}

	public static CardDatabase LoadFromJson(string json)
	{
		List<CardDto>? list = JsonSerializer.Deserialize<List<CardDto>>(json, DtoSerializerOptions) ?? throw new JsonException("Failed to deserialize card data: result was null.");
		Dictionary<string, CardDefinition> dictionary = new Dictionary<string, CardDefinition>(list.Count);
		foreach (CardDto item in list)
		{
			CardDefinition cardDefinition = MapToDefinition(item);
			dictionary[cardDefinition.DefinitionId] = cardDefinition;
		}
		CardDefinition value2;
		if (dictionary.TryGetValue("adathan,_celestial_messenger", out var value))
		{
			dictionary.TryAdd("adathyn_celestial_messenger", value);
			dictionary.TryAdd("adathan_celestial_messenger", value);
		}
		else if (dictionary.TryGetValue("adathyn_celestial_messenger", out value2))
		{
			dictionary.TryAdd("adathan,_celestial_messenger", value2);
			dictionary.TryAdd("adathan_celestial_messenger", value2);
		}
		CardDefinition value4;
		if (dictionary.TryGetValue("vactus,_spirit's_champion", out var value3))
		{
			dictionary.TryAdd("vactus_spirit's_champion", value3);
		}
		else if (dictionary.TryGetValue("vactus_spirit's_champion", out value4))
		{
			dictionary.TryAdd("vactus,_spirit's_champion", value4);
		}
		CardDefinition value6;
		if (dictionary.TryGetValue("tazian_dythanus", out var value5))
		{
			dictionary.TryAdd("taizan_dythanus", value5);
		}
		else if (dictionary.TryGetValue("taizan_dythanus", out value6))
		{
			dictionary.TryAdd("tazian_dythanus", value6);
		}
		return new CardDatabase
		{
			Definitions = dictionary
		};
	}

	public static string CleanUnicodeArtifacts(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(text, "\\\\u002[bB]", "+"), "\\\\u201[34]", "-"), "\\\\u00[aA]0", " "), "\\\\u0027", "'"), "\\\\u0022", "\"").Replace('–', '-').Replace('—', '-')
			.Replace('\u00a0', ' ')
			.Replace('‘', '\'')
			.Replace('’', '\'')
			.Replace('“', '"')
			.Replace('”', '"');
	}

	public static Dictionary<string, int> ParseSkillRatings(string text)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(text))
		{
			return dictionary;
		}
		string[] array = CleanUnicodeArtifacts(text).Split(new char[4] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (string.IsNullOrEmpty(text2))
			{
				continue;
			}
			foreach (Match item in SkillRatingRegex.Matches(text2))
			{
				string text3 = item.Groups[1].Value.Trim();
				if (!string.IsNullOrWhiteSpace(text3) && !text3.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any((string w) => ExcludedSkillWords.Contains(w)))
				{
					string text4 = text2.Substring(item.Index + item.Length).TrimStart().ToLowerInvariant();
					if (!text4.StartsWith("atk") && !text4.StartsWith("ac") && !text4.StartsWith("hp") && !text4.StartsWith("level") && !text4.StartsWith("wound") && !text4.StartsWith("damage") && !text4.StartsWith("strike") && !text4.StartsWith("cards") && int.TryParse(item.Groups[2].Value, out var result))
					{
						dictionary[text3] = result;
					}
				}
			}
		}
		return dictionary;
	}

	private static CardDefinition MapToDefinition(CardDto dto)
	{
		(CharacterClass? Primary, List<string> MultiClassTags) tuple = ParseClass(dto.Class);
		CharacterClass? item = tuple.Primary;
		List<string> item2 = tuple.MultiClassTags;
		Alignment? alignment = ParseAlignment(dto.Alignment);
		AttackValue attackValue = null;
		List<StrikeDto> strikes = dto.Strikes;
		if (strikes != null && strikes.Count > 0)
		{
			List<StrikeDescriptor> strikes2 = dto.Strikes.Select((StrikeDto s) => new StrikeDescriptor
			{
				Modifier = s.Bonus,
				IsRanged = s.IsRanged
			}).ToList();
			attackValue = new AttackValue
			{
				Strikes = strikes2,
				RawText = BuildAttackRawText(strikes2)
			};
		}
		string text = CleanUnicodeArtifacts(dto.Name);
		string text2 = CleanUnicodeArtifacts(dto.RulesText);
		List<string> list = new List<string>();
		List<string> tags = dto.Tags;
		int value;
		if (tags != null && tags.Count > 0)
		{
			foreach (string tag in dto.Tags)
			{
				string[] array = CleanUnicodeArtifacts(tag).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				for (value = 0; value < array.Length; value++)
				{
					string text3 = array[value].Trim();
					if (!string.IsNullOrEmpty(text3) && !list.Contains(text3, StringComparer.OrdinalIgnoreCase))
					{
						list.Add(text3);
					}
				}
			}
		}
		List<string> list2 = new List<string>(list);
		if (item2.Count > 0)
		{
			foreach (string item3 in item2)
			{
				if (!list2.Contains(item3, StringComparer.OrdinalIgnoreCase))
				{
					list2.Add(item3);
				}
			}
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		string key;
		foreach (string item4 in list)
		{
			foreach (KeyValuePair<string, int> item5 in ParseSkillRatings(item4))
			{
				item5.Deconstruct(out key, out value);
				string key2 = key;
				int value2 = value;
				dictionary[key2] = value2;
			}
		}
		foreach (KeyValuePair<string, int> item6 in ParseSkillRatings(text2))
		{
			item6.Deconstruct(out key, out value);
			string key3 = key;
			int value3 = value;
			if (!dictionary.ContainsKey(key3))
			{
				dictionary[key3] = value3;
			}
		}
		foreach (string key4 in dictionary.Keys)
		{
			if (!list2.Contains(key4, StringComparer.OrdinalIgnoreCase))
			{
				list2.Add(key4);
			}
		}
		Dictionary<string, int> skillRequirements = PlayCardAction.ExtractSkillRequirements(text2, list2);
		bool flag = dto.Type == CardType.Character;
		string text4 = dto.CharacterType?.Trim() ?? string.Empty;
		bool flag2 = flag && (text4.Equals("Warlord", StringComparison.OrdinalIgnoreCase) || dto.IsWarlord == true || (dto.Tags != null && dto.Tags.Any((string t) => t.Equals("Warlord", StringComparison.OrdinalIgnoreCase))) || (!string.IsNullOrWhiteSpace(dto.Class) && dto.Class.Equals("Warlord", StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrWhiteSpace(dto.Subtype) && dto.Subtype.Equals("Warlord", StringComparison.OrdinalIgnoreCase)) || KnownWarlordDefinitionIds.Contains(dto.DefinitionId));
		if (flag2 && !list2.Contains("Warlord", StringComparer.OrdinalIgnoreCase))
		{
			list2.Add("Warlord");
		}
		int? num = dto.AC;
		if (dto.DefinitionId.Equals("crownland_strategist", StringComparison.OrdinalIgnoreCase) && num == 4)
		{
			num = 14;
		}
		return new CardDefinition
		{
			DefinitionId = dto.DefinitionId,
			Name = ((!string.IsNullOrWhiteSpace(text)) ? text : dto.DefinitionId),
			Type = dto.Type,
			Class = item,
			Alignment = alignment,
			Level = dto.Level,
			ArmorClass = (flag ? num : ((int?)null)),
			Skill = (flag ? dto.Skill : ((int?)null)),
			Attack = (flag ? attackValue : null),
			BaseHitPoints = (flag ? dto.BaseHp : ((int?)null)),
			Abilities = RulesParser.Parse(text2, dto.Name),
			RulesText = text2,
			CharacterType = ((!string.IsNullOrWhiteSpace(text4)) ? text4 : (flag2 ? "Warlord" : string.Empty)),
			IsWarlord = flag2,
			Traits = list2,
			SkillRatings = dictionary,
			SkillRequirements = skillRequirements
		};
	}

	private static (CharacterClass? Primary, List<string> MultiClassTags) ParseClass(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return (Primary: null, MultiClassTags: new List<string>());
		}
		if (!raw.Contains('/'))
		{
			if (!Enum.TryParse<CharacterClass>(raw, ignoreCase: true, out var result))
			{
				CharacterClass? item = CharacterClass.Classless;
				int num = 1;
				List<string> list = new List<string>(num);
				CollectionsMarshal.SetCount(list, num);
				CollectionsMarshal.AsSpan(list)[0] = "UnknownClass:" + raw;
				return (Primary: item, MultiClassTags: list);
			}
			return (Primary: result, MultiClassTags: new List<string>());
		}
		string[] array = raw.Split('/');
		CharacterClass? characterClass = null;
		List<string> list2 = new List<string>();
		string[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			string text = array2[num].Trim();
			if (Enum.TryParse<CharacterClass>(text, ignoreCase: true, out var result2))
			{
				CharacterClass valueOrDefault = characterClass.GetValueOrDefault();
				if (!characterClass.HasValue)
				{
					valueOrDefault = result2;
					characterClass = valueOrDefault;
				}
				list2.Add("MultiClass:" + text);
			}
		}
		return (Primary: characterClass ?? CharacterClass.Classless, MultiClassTags: list2);
	}

	private static Alignment? ParseAlignment(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		if (!Enum.TryParse<Alignment>(raw, ignoreCase: true, out var result))
		{
			return null;
		}
		return result;
	}

	private static string BuildAttackRawText(List<StrikeDescriptor> strikes)
	{
		IEnumerable<string> values = strikes.Select((StrikeDescriptor s) =>
		{
			string value = ((s.Modifier >= 0) ? "+" : "");
			string value2 = (s.IsRanged ? " Ranged" : "");
			return $"{value}{s.Modifier}{value2}";
		});
		return string.Join("/", values);
	}
}
