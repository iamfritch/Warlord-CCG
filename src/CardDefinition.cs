using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using WarlordEngine.Abilities;

namespace WarlordEngine;

public sealed record CardDefinition
{
	public required string DefinitionId { get; init; }

	public required string Name { get; init; }

	public required CardType Type { get; init; }

	public CharacterClass? Class { get; init; }

	public Alignment? Alignment { get; init; }

	public string Faction { get; init; } = string.Empty;

	public bool HasReach
	{
		get
		{
			if (!Traits.Any((string t) => t.Equals("Reach", StringComparison.OrdinalIgnoreCase)) && !RulesText.Contains("Reach", StringComparison.OrdinalIgnoreCase) && !RulesText.Contains("strike 1 rank further", StringComparison.OrdinalIgnoreCase))
			{
				return RulesText.Contains("strike into an adjacent rank", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	public int Level { get; init; }

	public int? ArmorClass { get; init; }

	public int? Skill { get; init; }

	public AttackValue? Attack { get; init; }

	public int? BaseHitPoints { get; init; }

	public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = Array.Empty<AbilityDefinition>();

	public string RulesText { get; init; } = string.Empty;

	public string CharacterType { get; init; } = string.Empty;

	public bool IsWarlord { get; init; }

	public IReadOnlyList<string> Traits { get; init; } = Array.Empty<string>();

	public Dictionary<string, int> SkillRatings { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, int> SkillRequirements { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	public string GetEffectiveFaction()
	{
		if (!string.IsNullOrWhiteSpace(Faction))
		{
			return Faction;
		}
		foreach (string trait in Traits)
		{
			if (trait.Contains("Free Kingdoms", StringComparison.OrdinalIgnoreCase) || trait.Contains("Deverenian", StringComparison.OrdinalIgnoreCase) || trait.Contains("Nothrog", StringComparison.OrdinalIgnoreCase) || trait.Contains("Mercenary", StringComparison.OrdinalIgnoreCase) || trait.Contains("Dwarven", StringComparison.OrdinalIgnoreCase) || trait.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) || trait.Contains("Elven", StringComparison.OrdinalIgnoreCase) || trait.Contains("Elf", StringComparison.OrdinalIgnoreCase))
			{
				return trait;
			}
		}
		return Alignment?.ToString() ?? "Neutral";
	}

	public bool IsUsableByClass(CharacterClass? charClass, IReadOnlyList<string>? charTraits = null)
	{
		if (!Class.HasValue || Class == CharacterClass.Classless)
		{
			return true;
		}
		if (charClass.HasValue && charClass == Class)
		{
			return true;
		}
		if (charTraits != null && charTraits.Any((string t) => t.Equals($"MultiClass:{Class}", StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		return false;
	}

	public bool IsUsableByAlignment(Alignment? targetAlignment)
	{
		if (!Alignment.HasValue || !targetAlignment.HasValue)
		{
			return true;
		}
		if (Alignment == WarlordEngine.Alignment.Neutral || targetAlignment == WarlordEngine.Alignment.Neutral)
		{
			return true;
		}
		return Alignment == targetAlignment;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private CardDefinition(CardDefinition original)
	{
		DefinitionId = original.DefinitionId;
		Name = original.Name;
		Type = original.Type;
		Class = original.Class;
		Alignment = original.Alignment;
		Faction = original.Faction;
		Level = original.Level;
		ArmorClass = original.ArmorClass;
		Skill = original.Skill;
		Attack = original.Attack;
		BaseHitPoints = original.BaseHitPoints;
		Abilities = original.Abilities;
		RulesText = original.RulesText;
		CharacterType = original.CharacterType;
		IsWarlord = original.IsWarlord;
		Traits = original.Traits;
		SkillRatings = original.SkillRatings;
		SkillRequirements = original.SkillRequirements;
	}
}
