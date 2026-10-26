using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WarlordEngine.Abilities;
using WarlordEngine.Effects;
using WarlordEngine.Engine;
using Xunit;

namespace WarlordEngine.Tests;

public sealed class RulesParserTests
{
	[Fact]
	public void Parse_SpendOrder_With_Target_Text()
	{
		List<AbilityDefinition> list = RulesParser.Parse("Spend Order: Target a character within two ranks: Return an item equipped to the target to its owner's hand.");
		Assert.Single(list);
		AbilityDefinition abilityDefinition = list[0];
		Assert.Equal(AbilityTrigger.SpendOrder, abilityDefinition.Trigger);
		Assert.Equal("Spend Order: Target a character within two ranks: Return an item equipped to the target to its owner's hand.", abilityDefinition.Description);
		Assert.True(abilityDefinition.RequiresSpend);
	}

	[Fact]
	public void Parse_EnterReact_With_DrawCard_Effect()
	{
		List<AbilityDefinition> list = RulesParser.Parse("Enter React: Shuffle a card from your hand into your deck: Draw a card.");
		Assert.Single(list);
		AbilityDefinition abilityDefinition = list[0];
		Assert.Equal(AbilityTrigger.EnterReact, abilityDefinition.Trigger);
		Assert.Single(abilityDefinition.Effects);
		DrawCardEffect drawCardEffect = Assert.IsType<DrawCardEffect>(abilityDefinition.Effects[0]);
		Assert.Equal(1, drawCardEffect.Amount);
	}

	[Fact]
	public void Parse_Order_With_ATK_Buff()
	{
		List<AbilityDefinition> list = RulesParser.Parse("Order: This character gains +2 ATK and True Strike this turn.");
		Assert.Single(list);
		AbilityDefinition abilityDefinition = list[0];
		Assert.Equal(AbilityTrigger.Order, abilityDefinition.Trigger);
		Assert.False(abilityDefinition.RequiresSpend);
		Assert.Single(abilityDefinition.Effects);
		ModifyStatEffect modifyStatEffect = Assert.IsType<ModifyStatEffect>(abilityDefinition.Effects[0]);
		Assert.Equal("ATK", modifyStatEffect.StatName);
		Assert.Equal(2, modifyStatEffect.Amount);
	}

	[Theory]
	[InlineData(new object[]
	{
		"React: Deal 3 damage to attacker.",
		AbilityTrigger.React
	})]
	[InlineData(new object[]
	{
		"Spend React: +2 AC this turn.",
		AbilityTrigger.SpendReact
	})]
	[InlineData(new object[]
	{
		"Death React: Draw 2 cards.",
		AbilityTrigger.DeathReact
	})]
	public void Parse_Various_Triggers_And_Effects(string text, AbilityTrigger expectedTrigger)
	{
		List<AbilityDefinition> list = RulesParser.Parse(text);
		Assert.Single(list);
		Assert.Equal(expectedTrigger, list[0].Trigger);
	}

	[Fact]
	public void Analyze_StarterDecks_CardRulesCoverage()
	{
		string[] source = new string[5]
		{
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "DataParser", "cards.json"),
			Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "DataParser", "cards.json"),
			"C:\\Users\\iamfr\\Documents\\WarlordCCG\\DataParser\\cards.json"
		};
		string text = source.FirstOrDefault(File.Exists);
		if (text == null)
		{
			return;
		}
		CardDatabase cardDatabase = DatabaseLoader.LoadFromFile(text);
		IReadOnlyList<Deck> sixFactionStarterDecks = PresetDeckFactory.GetSixFactionStarterDecks(cardDatabase);
		HashSet<string> hashSet = new HashSet<string>();
		foreach (Deck item in sixFactionStarterDecks)
		{
			hashSet.Add(item.WarlordDefinitionId);
			foreach (string startingCharacterDefinitionId in item.StartingCharacterDefinitionIds)
			{
				hashSet.Add(startingCharacterDefinitionId);
			}
			foreach (string cardDefinitionId in item.CardDefinitionIds)
			{
				hashSet.Add(cardDefinitionId);
			}
		}
		int count = hashSet.Count;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (string item2 in hashSet)
		{
			CardDefinition definition = cardDatabase.GetDefinition(item2);
			if (definition == null || string.IsNullOrWhiteSpace(definition.RulesText))
			{
				continue;
			}
			num++;
			List<AbilityDefinition> list = RulesParser.Parse(definition.RulesText);
			if (list.Count > 0)
			{
				num2 += list.Count;
				foreach (AbilityDefinition item3 in list)
				{
					num3 += item3.Effects.Count;
					if (item3.Effects.Count == 0)
					{
						string text2 = (definition.RulesText.Contains(':') ? definition.RulesText.Substring(definition.RulesText.IndexOf(':') + 1).Trim() : definition.RulesText);
						string key = ((text2.Length > 40) ? (text2.Substring(0, 40) + "...") : text2);
						dictionary[key] = dictionary.GetValueOrDefault(key, 0) + 1;
					}
				}
			}
			else
			{
				string key2 = ((definition.RulesText.Length > 40) ? (definition.RulesText.Substring(0, 40) + "...") : definition.RulesText);
				dictionary[key2] = dictionary.GetValueOrDefault(key2, 0) + 1;
			}
		}
		Assert.True(count > 0);
		Console.WriteLine($"[StarterDecks Analysis] Total unique cards: {count}, With RulesText: {num}");
		Console.WriteLine($"[StarterDecks Analysis] Parsed abilities: {num2}, Parsed effects: {num3}");
		Console.WriteLine($"[StarterDecks Analysis] Unparsed patterns count: {dictionary.Count}");
		foreach (var (value, value2) in dictionary.OrderByDescending((KeyValuePair<string, int> kv) => kv.Value).Take(10))
		{
			Console.WriteLine($"  Pattern ({value2}x): {value}");
		}
	}
}
