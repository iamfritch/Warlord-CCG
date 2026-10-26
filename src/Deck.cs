using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using WarlordEngine.Actions;

namespace WarlordEngine;

public sealed record Deck
{
	public required string DeckName { get; init; }

	public string? Faction { get; init; }

	public required string WarlordDefinitionId { get; init; }

	public required IReadOnlyList<string> CardDefinitionIds { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> StartingCharacterDefinitionIds { get; init; } = Array.Empty<string>();

	public int TotalCardCount => CardDefinitionIds.Count + StartingCharacterDefinitionIds.Count + ((!string.IsNullOrWhiteSpace(WarlordDefinitionId)) ? 1 : 0);

	public ActionResult Validate(CardDatabase cardDb, int minCardCount = 50)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(DeckName))
		{
			list.Add("Deck name cannot be empty.");
		}
		if (string.IsNullOrWhiteSpace(WarlordDefinitionId))
		{
			list.Add("Deck must specify a Warlord definition ID.");
		}
		else
		{
			CardDefinition definition = cardDb.GetDefinition(WarlordDefinitionId);
			if (definition == null)
			{
				list.Add("Warlord definition '" + WarlordDefinitionId + "' was not found in the card database.");
			}
			else if (!definition.IsWarlord)
			{
				list.Add($"Card '{definition.Name}' ({WarlordDefinitionId}) is not a Warlord character.");
			}
		}
		int totalCardCount = TotalCardCount;
		int num = ((StartingCharacterDefinitionIds.Count > 0) ? totalCardCount : CardDefinitionIds.Count);
		if (num < minCardCount)
		{
			list.Add($"Deck '{DeckName}' contains {num} cards, which is below the minimum required size of {minCardCount}.");
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, bool> isEpicMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		int actionCount = 0;
		int itemCount = 0;
		foreach (string cardDefinitionId in CardDefinitionIds)
		{
			CardDefinition definition2 = cardDb.GetDefinition(cardDefinitionId);
			if (definition2 == null)
			{
				list.Add($"Card definition '{cardDefinitionId}' in deck '{DeckName}' was not found in database.");
				dictionary[cardDefinitionId] = dictionary.GetValueOrDefault(cardDefinitionId, 0) + 1;
			}
			else
			{
				string key = definition2.Name;
				dictionary[key] = dictionary.GetValueOrDefault(key, 0) + 1;
				if (definition2.Type == CardType.Action)
				{
					actionCount++;
				}
				else if (definition2.Type == CardType.Item)
				{
					itemCount++;
				}
				bool isEpic = definition2.Traits.Any((string t) => t.Equals("Epic", StringComparison.OrdinalIgnoreCase)) || Regex.IsMatch(definition2.RulesText ?? "", @"\bEpic\b", RegexOptions.IgnoreCase);
				if (isEpic)
				{
					isEpicMap[key] = true;
				}
			}
		}
		foreach (string startingCharacterDefinitionId in StartingCharacterDefinitionIds)
		{
			if (cardDb.GetDefinition(startingCharacterDefinitionId) == null)
			{
				list.Add($"Starting character definition '{startingCharacterDefinitionId}' in deck '{DeckName}' was not found in database.");
			}
		}
		foreach (var (value, num3) in dictionary)
		{
			if (isEpicMap.GetValueOrDefault(value, false) && num3 > 1)
			{
				list.Add($"Deck '{DeckName}' contains {num3} copies of Epic card '{value}', which exceeds the maximum limit of 1 copy.");
			}
			else if (num3 > 3)
			{
				list.Add($"Deck '{DeckName}' contains {num3} copies of card definition '{value}', which exceeds the maximum limit of 3 copies.");
			}
		}
		int totalDeckCards = CardDefinitionIds.Count;
		if (totalDeckCards > 0)
		{
			if (actionCount > totalDeckCards * 0.5)
			{
				list.Add($"Deck '{DeckName}' contains {actionCount} Action cards, which exceeds the maximum limit of 50% of the deck.");
			}
			if (itemCount > totalDeckCards * 0.5)
			{
				list.Add($"Deck '{DeckName}' contains {itemCount} Item cards, which exceeds the maximum limit of 50% of the deck.");
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private Deck(Deck original)
	{
		DeckName = original.DeckName;
		Faction = original.Faction;
		WarlordDefinitionId = original.WarlordDefinitionId;
		CardDefinitionIds = original.CardDefinitionIds;
		StartingCharacterDefinitionIds = original.StartingCharacterDefinitionIds;
	}
}
