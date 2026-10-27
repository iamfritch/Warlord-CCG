using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WarlordEngine.Abilities;
using WarlordEngine.Effects;

namespace WarlordEngine.Engine;

public static class RulesParser
{
	private delegate IEffect? EffectMatcher(string text);

	private static readonly Dictionary<string, AbilityTrigger> TriggerMap = new Dictionary<string, AbilityTrigger>(StringComparer.OrdinalIgnoreCase)
	{
		["Order"] = AbilityTrigger.Order,
		["Spend Order"] = AbilityTrigger.SpendOrder,
		["Limited Order"] = AbilityTrigger.Order,
		["Spend Limited Order"] = AbilityTrigger.SpendOrder,
		["Destroy Order"] = AbilityTrigger.Order,
		["Destroy Limited Order"] = AbilityTrigger.Order,
		["Roll Order"] = AbilityTrigger.Order,
		["React"] = AbilityTrigger.React,
		["Spend React"] = AbilityTrigger.SpendReact,
		["Limited React"] = AbilityTrigger.React,
		["Spend Limited React"] = AbilityTrigger.SpendReact,
		["Destroy React"] = AbilityTrigger.React,
		["Destroy Limited React"] = AbilityTrigger.React,
		["Death React"] = AbilityTrigger.DeathReact,
		["Enter React"] = AbilityTrigger.EnterReact,
		["Kill React"] = AbilityTrigger.React
	};

	private static readonly List<EffectMatcher> EffectMatchers;

	public static List<AbilityDefinition> Parse(string rulesText)
	{
		return Parse(rulesText, null);
	}

	public static List<AbilityDefinition> Parse(string rulesText, string? cardName)
	{
		List<AbilityDefinition> list = new List<AbilityDefinition>();
		if (string.IsNullOrWhiteSpace(rulesText))
		{
			return list;
		}
		rulesText = DatabaseLoader.CleanUnicodeArtifacts(rulesText);
		rulesText = rulesText.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\r\n", "\n")
			.Replace("\r", "\n");
		if (rulesText.Contains("Roll Order:", StringComparison.OrdinalIgnoreCase))
		{
			AbilityDefinition abilityDefinition = ParseRollOrderAbility(rulesText, cardName);
			if (abilityDefinition != null)
			{
				list.Add(abilityDefinition);
				return list;
			}
		}
		string[] array = rulesText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		List<string> list2 = new List<string>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			int num = text.IndexOf(':');
			if (num > 0 && TriggerMap.ContainsKey(text.Substring(0, num).Trim()))
			{
				list2.Add(text);
			}
		}
		List<string> list3 = new List<string>();
		List<string> list4;
		if (list2.Count <= 1)
		{
			if (list2.Count != 1)
			{
				int i = 1;
				list4 = new List<string>(i);
				CollectionsMarshal.SetCount(list4, i);
				CollectionsMarshal.AsSpan(list4)[0] = rulesText;
			}
			else
			{
				list4 = list2;
			}
		}
		else
		{
			list4 = list2;
		}
		bool flag = Regex.IsMatch(rulesText, "wounds (?:from this (?:action|strike) )?may not be (?:reduced|prevented)|prevented or reduced", RegexOptions.IgnoreCase);
		foreach (string item3 in list4)
		{
			Match match = Regex.Match(item3, "your\\s+choice\\s+of\\s+\\+?(\\d+)\\s+ATK\\s+or\\s+(?:\\+?\\d+\\s+)?AC", RegexOptions.IgnoreCase);
			Match match2 = Regex.Match(item3, "either\\s+\\+?(\\d+)\\s+ATK\\s+or\\s+\\+?(\\d+)\\s+AC", RegexOptions.IgnoreCase);
			Match match3 = Regex.Match(item3, "^(Spend Order|Order):\\s*(?:Target\\s+your\\s+wounded\\s+Dwarven\\s+Forges\\s+character:\\s*)?Unstun\\s+(?:the\\s+target|a\\s+wounded\\s+Dwarven\\s+Forges\\s+character(?:\\s+in\\s+your\\s+first\\s+or\\s+second\\s+rank)?),\\s*or\\s+(the\\s+target\\s+performs.*)$", RegexOptions.IgnoreCase);
			Match match4 = Regex.Match(item3, "^Order:\\s*If\\s+(?:the\\s+performer|this\\s+character)\\s+is\\s+a\\s+wizard,\\s*(.*?)\\.\\s*If\\s+(?:the\\s+performer|this\\s+character)\\s+is\\s+a\\s+fighter,\\s*(.*?)\\.?$", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				int value = int.Parse(match.Groups[1].Value);
				string text2 = Regex.Replace(item3, "your\\s+choice\\s+of\\s+\\+?\\d+\\s+ATK\\s+or\\s+(?:\\+?\\d+\\s+)?AC", $"+{value} ATK", RegexOptions.IgnoreCase);
				string text3 = Regex.Replace(item3, "your\\s+choice\\s+of\\s+\\+?\\d+\\s+ATK\\s+or\\s+(?:\\+?\\d+\\s+)?AC", $"+{value} AC", RegexOptions.IgnoreCase);
				list3.Add(text2 + $" (+{value} ATK)");
				list3.Add(text3 + $" (+{value} AC)");
			}
			else if (match2.Success)
			{
				int value2 = int.Parse(match2.Groups[1].Value);
				int value3 = int.Parse(match2.Groups[2].Value);
				string item = Regex.Replace(item3, "either\\s+\\+?\\d+\\s+ATK\\s+or\\s+\\+?\\d+\\s+AC", $"+{value2} ATK", RegexOptions.IgnoreCase);
				string item2 = Regex.Replace(item3, "either\\s+\\+?\\d+\\s+ATK\\s+or\\s+\\+?\\d+\\s+AC", $"+{value3} AC", RegexOptions.IgnoreCase);
				list3.Add(item);
				list3.Add(item2);
			}
			else if (match3.Success)
			{
				string value4 = match3.Groups[1].Value;
				string value5 = match3.Groups[2].Value;
				list3.Add(value4 + ": Unstun a wounded Dwarven Forges character in your first or second rank.");
				list3.Add(value4 + ": Your target wounded Dwarven Forges character " + value5);
			}
			else if (match4.Success)
			{
				string value6 = match4.Groups[1].Value;
				string value7 = match4.Groups[2].Value;
				list3.Add("Order (Wizard): " + value6);
				list3.Add("Order (Fighter): " + value7);
			}
			else
			{
				list3.Add(item3);
			}
		}
		foreach (string item4 in list3)
		{
			AbilityDefinition abilityDefinition2 = ParseSingleAbility(item4, item4, cardName);
			if (!(abilityDefinition2 != null))
			{
				continue;
			}
			if (flag)
			{
				List<IEffect> effects = abilityDefinition2.Effects.Select((IEffect e) => (!(e is PerformStrikeEffect performStrikeEffect)) ? e : performStrikeEffect with
				{
					CannotReduceWounds = true
				}).ToList();
				abilityDefinition2 = abilityDefinition2 with
				{
					Effects = effects
				};
			}
			list.Add(abilityDefinition2);
		}
		return list;
	}

	private static AbilityDefinition? ParseRollOrderAbility(string text, string? cardName)
	{
		List<RollTableBranch> list = new List<RollTableBranch>();
		foreach (Match item in Regex.Matches(text, "(?:^|\\n)\\s*(\\d+)(?:-(\\d+))?:\\s*(.*?)(?=(?:\\n\\s*\\d+(?:-\\d+)?:)|$)", RegexOptions.Singleline))
		{
			int num = int.Parse(item.Groups[1].Value);
			int maxRoll = ((item.Groups[2].Success && !string.IsNullOrEmpty(item.Groups[2].Value)) ? int.Parse(item.Groups[2].Value) : num);
			string text2 = item.Groups[3].Value.Trim();
			List<IEffect> list2 = new List<IEffect>();
			foreach (EffectMatcher effectMatcher in EffectMatchers)
			{
				IEffect effect = effectMatcher(text2);
				if (effect != null)
				{
					list2.Add(effect);
				}
			}
			list.Add(new RollTableBranch(num, maxRoll, list2));
		}
		if (list.Count == 0)
		{
			return null;
		}
		TargetScope targetScope = TargetScope.Self;
		if (Regex.IsMatch(text, "Target your other character", RegexOptions.IgnoreCase))
		{
			targetScope = TargetScope.OtherInSameRank;
		}
		else if (Regex.IsMatch(text, "Target an opposing character|opponent", RegexOptions.IgnoreCase))
		{
			targetScope = TargetScope.Opposing;
		}
		return new AbilityDefinition
		{
			Trigger = AbilityTrigger.Order,
			Effects = [new RollTableEffect(list)],
			Description = text,
			Timing = ReactTiming.None,
			TargetScope = targetScope
		};
	}

	private static AbilityDefinition? ParseSingleAbility(string text, string description, string? cardName)
	{
		int num = text.IndexOf(':');
		if (num <= 0)
		{
			return null;
		}
		string text2 = text.Substring(0, num).Trim();
		string text3 = text.Substring(num + 1).Trim();
		string text4 = null;
		if (text2.Contains("(Wizard)", StringComparison.OrdinalIgnoreCase))
		{
			text4 = "Wizard";
			text2 = text2.Replace("(Wizard)", "", StringComparison.OrdinalIgnoreCase).Trim();
		}
		else if (text2.Contains("(Fighter)", StringComparison.OrdinalIgnoreCase))
		{
			text4 = "Fighter";
			text2 = text2.Replace("(Fighter)", "", StringComparison.OrdinalIgnoreCase).Trim();
		}
		if (!TriggerMap.TryGetValue(text2, out var value))
		{
			return null;
		}
		bool destroysSource = text2.StartsWith("Destroy", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text3, "^Destroy\\s+this\\s+(?:item|card):\\s*", RegexOptions.IgnoreCase) || Regex.IsMatch(text3, "^Discard\\s+this\\s+(?:item|card):\\s*", RegexOptions.IgnoreCase) || (cardName != null && cardName.Equals("Strongarm Stout", StringComparison.OrdinalIgnoreCase));
		if (Regex.IsMatch(text3, "^Destroy\\s+this\\s+(?:item|card):\\s*", RegexOptions.IgnoreCase))
		{
			text3 = Regex.Replace(text3, "^Destroy\\s+this\\s+(?:item|card):\\s*", "", RegexOptions.IgnoreCase);
		}
		else if (Regex.IsMatch(text3, "^Discard\\s+this\\s+(?:item|card):\\s*", RegexOptions.IgnoreCase))
		{
			text3 = Regex.Replace(text3, "^Discard\\s+this\\s+(?:item|card):\\s*", "", RegexOptions.IgnoreCase);
		}
		int discardHandCost = 0;
		if (Regex.IsMatch(text3, "^Discard\\s+(?:a|1)\\s+card:\\s*", RegexOptions.IgnoreCase))
		{
			discardHandCost = 1;
			text3 = Regex.Replace(text3, "^Discard\\s+(?:a|1)\\s+card:\\s*", "", RegexOptions.IgnoreCase);
		}
		bool requiresSpendAdjacentAlly = false;
		if (Regex.IsMatch(text3, "^Spend\\s+your\\s+adjacent\\s+character:\\s*", RegexOptions.IgnoreCase))
		{
			requiresSpendAdjacentAlly = true;
			text3 = Regex.Replace(text3, "^Spend\\s+your\\s+adjacent\\s+character:\\s*", "", RegexOptions.IgnoreCase);
		}
		bool requiresSpendAllyInRank = false;
		if (Regex.IsMatch(text3, "^Spend\\s+a\\s+character\\s+in\\s+this\\s+rank:\\s*", RegexOptions.IgnoreCase))
		{
			requiresSpendAllyInRank = true;
			text3 = Regex.Replace(text3, "^Spend\\s+a\\s+character\\s+in\\s+this\\s+rank:\\s*", "", RegexOptions.IgnoreCase);
		}
		int banishActionFromDiscardCost = 0;
		if (Regex.IsMatch(text3, "(?:^|,\\s*|and\\s+)banish\\s+(?:two|2)\\s+action\\s+cards\\s+from\\s+your\\s+discard\\s+pile:\\s*", RegexOptions.IgnoreCase))
		{
			banishActionFromDiscardCost = 2;
			text3 = Regex.Replace(text3, "(?:^|,\\s*|and\\s+)banish\\s+(?:two|2)\\s+action\\s+cards\\s+from\\s+your\\s+discard\\s+pile:\\s*", "", RegexOptions.IgnoreCase);
		}
		bool discardsOpponentTopDeck = false;
		if (Regex.IsMatch(text3, "^Discard\\s+the\\s+top\\s+card\\s+of\\s+an\\s+opponent's\\s+deck:\\s*", RegexOptions.IgnoreCase))
		{
			discardsOpponentTopDeck = true;
			text3 = Regex.Replace(text3, "^Discard\\s+the\\s+top\\s+card\\s+of\\s+an\\s+opponent's\\s+deck:\\s*", "", RegexOptions.IgnoreCase);
		}
		bool stunSelfCost = false;
		if (Regex.IsMatch(text3, "^Stun\\s+(?:TFK Brigand|this character|himself|herself):\\s*", RegexOptions.IgnoreCase))
		{
			stunSelfCost = true;
			text3 = Regex.Replace(text3, "^Stun\\s+(?:TFK Brigand|this character|himself|herself):\\s*", "", RegexOptions.IgnoreCase);
		}
		if (Regex.IsMatch(text3, "(?:^|,\\s*)spend\\s+Ardian\\s+Greathawk:\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendReact;
			text3 = Regex.Replace(text3, "(?:^|,\\s*)spend\\s+Ardian\\s+Greathawk:\\s*", " ", RegexOptions.IgnoreCase).Trim();
		}
		else if (Regex.IsMatch(text3, "(?:^|,\\s*)spend\\s+Nothrog\\s+S'sike:\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendReact;
			text3 = Regex.Replace(text3, "(?:^|,\\s*)spend\\s+Nothrog\\s+S'sike:\\s*", " ", RegexOptions.IgnoreCase).Trim();
		}
		else if (Regex.IsMatch(text3, "(?:^|,\\s*)spend\\s+Shield\\s+of\\s+Spurning:\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendReact;
			text3 = Regex.Replace(text3, "(?:^|,\\s*)spend\\s+Shield\\s+of\\s+Spurning:\\s*", " ", RegexOptions.IgnoreCase).Trim();
		}
		else if (Regex.IsMatch(text3, "^Spend\\s+Hunting\\s+Staff\\s+and\\s+target\\s+an\\s+adjacent\\s+character:\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendOrder;
			text3 = Regex.Replace(text3, "^Spend\\s+Hunting\\s+Staff\\s+and\\s+target\\s+an\\s+adjacent\\s+character:\\s*", "", RegexOptions.IgnoreCase);
		}
		else if (Regex.IsMatch(text3, "^Spend\\s+Unicorn\\s+of\\s+Thun(?:\\s+and\\s+)?", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendOrder;
			text3 = Regex.Replace(text3, "^Spend\\s+Unicorn\\s+of\\s+Thun(?:\\s+and\\s+)?", "", RegexOptions.IgnoreCase);
			if (text3.StartsWith(":"))
			{
				text3 = text3.Substring(1).Trim();
			}
		}
		else if (value == AbilityTrigger.Order && Regex.IsMatch(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendOrder;
			text3 = Regex.Replace(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", "", RegexOptions.IgnoreCase);
		}
		else if (value == AbilityTrigger.React && Regex.IsMatch(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", RegexOptions.IgnoreCase))
		{
			value = AbilityTrigger.SpendReact;
			text3 = Regex.Replace(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", "", RegexOptions.IgnoreCase);
		}
		else if (value == AbilityTrigger.SpendOrder && Regex.IsMatch(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", RegexOptions.IgnoreCase))
		{
			text3 = Regex.Replace(text3, "^Spend\\s+(?:this\\s+card|[A-Z][a-zA-Z\\s']+):\\s*", "", RegexOptions.IgnoreCase);
		}
		bool isLimited = text2.Contains("Limited", StringComparison.OrdinalIgnoreCase);
		List<IEffect> list = new List<IEffect>();
		foreach (EffectMatcher effectMatcher in EffectMatchers)
		{
			IEffect effect = effectMatcher(text3);
			if (effect != null)
			{
				list.Add(effect);
			}
		}
		(ReactTiming Timing, TargetScope Scope, int? MaxDist) tuple = ParseTimingAndScope(text, value, cardName);
		ReactTiming item = tuple.Timing;
		TargetScope targetScope = tuple.Scope;
		int? item2 = tuple.MaxDist;
		bool splitSecond = text.Contains("may not react", StringComparison.OrdinalIgnoreCase) || text.Contains("cannot react", StringComparison.OrdinalIgnoreCase);
		if (text4 == "Wizard")
		{
			targetScope = TargetScope.Friendly;
		}
		else if (text4 == "Fighter")
		{
			targetScope = TargetScope.Opposing;
		}
		else if (text3.Contains("Target your wounded Dwarven Forges character", StringComparison.OrdinalIgnoreCase))
		{
			targetScope = TargetScope.Friendly;
		}
		else if (text3.Contains("Target your non-stunned Dwarven Forges character", StringComparison.OrdinalIgnoreCase))
		{
			targetScope = TargetScope.Friendly;
		}
		else if (text3.Contains("Your two target characters attack", StringComparison.OrdinalIgnoreCase))
		{
			targetScope = TargetScope.Friendly;
		}
		else if (text3.Contains("Target opponent chooses", StringComparison.OrdinalIgnoreCase))
		{
			targetScope = TargetScope.Opposing;
		}
		else if (text3.Contains("Your target character performs a melee strike", StringComparison.OrdinalIgnoreCase))
		{
			targetScope = TargetScope.Friendly;
		}
		return new AbilityDefinition
		{
			Trigger = value,
			IsLimited = isLimited,
			DestroysSource = destroysSource,
			DiscardHandCost = discardHandCost,
			RequiresSpendAdjacentAlly = requiresSpendAdjacentAlly,
			RequiresSpendAllyInRank = requiresSpendAllyInRank,
			BanishActionFromDiscardCost = banishActionFromDiscardCost,
			DiscardsOpponentTopDeck = discardsOpponentTopDeck,
			StunSelfCost = stunSelfCost,
			RequiredPerformerClass = text4,
			SplitSecond = splitSecond,
			Effects = list,
			Description = description,
			Timing = item,
			TargetScope = targetScope,
			MaxRankDistance = item2
		};
	}

	public static (ReactTiming Timing, TargetScope Scope, int? MaxDist) ParseTimingAndScope(string text, AbilityTrigger trigger, string? cardName = null)
	{
		if (trigger != AbilityTrigger.React && trigger != AbilityTrigger.SpendReact && trigger != AbilityTrigger.DeathReact && trigger != AbilityTrigger.EnterReact)
		{
			return (Timing: ReactTiming.None, Scope: TargetScope.Any, MaxDist: null);
		}
		string text2 = text.ToLowerInvariant();
		string text3 = cardName?.Trim().ToLowerInvariant() ?? string.Empty;
		ReactTiming item = ReactTiming.None;
		if (trigger == AbilityTrigger.EnterReact || text2.Contains("enter react") || Regex.IsMatch(text2, "\\bafter\\s+.*?\\benters\\s+play\\b"))
		{
			item = ReactTiming.Enter;
		}
		else if (Regex.IsMatch(text2, "\\bbefore\\s+.*?\\b(?:dies|is killed|would be stunned or killed)\\b"))
		{
			item = ReactTiming.BeforeDies;
		}
		else if (text2.Contains("kill react") || text2.Contains("killed an opposing character") || Regex.IsMatch(text2, "\\bafter\\s+.*?\\bstrike that killed\\b"))
		{
			item = ReactTiming.Kill;
		}
		else if (trigger == AbilityTrigger.DeathReact || Regex.IsMatch(text2, "\\bafter\\s+.*?\\b(?:dies|killed)\\b"))
		{
			item = ReactTiming.Death;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+your\\s+wizard\\s+casts\\s+a\\s+spell\\b"))
		{
			item = ReactTiming.AfterWizardCastsSpell;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\b(?:performs a (?:melee\\s+)?strike|attacks)\\b"))
		{
			item = ReactTiming.AfterAttack;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\b(?:targeted with a strike|targets this character|is targeted by)\\b"))
		{
			item = ReactTiming.AfterTargetedWithStrike;
		}
		else if (Regex.IsMatch(text2, "\\bbefore\\s+.*?\\b(?:strike roll|makes a .*?strike)\\b"))
		{
			item = ReactTiming.BeforeStrikeRoll;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\bmoves?\\b"))
		{
			item = ReactTiming.AfterMove;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\bstrike that missed\\b"))
		{
			item = ReactTiming.AfterAttack;
		}
		else if (Regex.IsMatch(text2, "\\bbefore\\s+.*?\\bsuffers wounds?\\b"))
		{
			item = ReactTiming.BeforeSuffersWounds;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\bsuffers wounds?\\b"))
		{
			item = ReactTiming.AfterSuffersWounds;
		}
		else if (Regex.IsMatch(text2, "\\bafter\\s+.*?\\b(?:hits with|strike that hit)\\b"))
		{
			item = ReactTiming.AfterStrikeHit;
		}
		TargetScope item2 = TargetScope.Any;
		int? item3 = null;
		if (Regex.IsMatch(text2, @"\bafter\s+(?:target\s+)?opposing\s+character\s+moves\b"))
		{
			item2 = TargetScope.Opposing;
			if (Regex.IsMatch(text2, @"\bwithin\s+(?:one|1)\s+rank\b"))
			{
				item3 = 1;
			}
			else if (Regex.IsMatch(text2, @"\bwithin\s+(?:two|2)\s+ranks\b"))
			{
				item3 = 2;
			}
		}
		else if (Regex.IsMatch(text2, @"\bafter\s+.*?\bperforms a (?:melee\s+)?strike targeting this character\b"))
		{
			item2 = TargetScope.Opposing;
		}
		else if (text2.Contains("another character in this rank"))
		{
			item2 = TargetScope.OtherInSameRank;
		}
		else if (text2.Contains("targeting this character") || text2.Contains("targets this character") || text2.Contains("this character is targeted") || text2.Contains("this character") || text2.Contains("this item") || (!string.IsNullOrEmpty(text3) && (Regex.IsMatch(text2, "\\bafter\\s+" + Regex.Escape(text3) + "\\s+moves\\b") || Regex.IsMatch(text2, "\\bbefore\\s+" + Regex.Escape(text3) + "\\s+(?:suffers|makes)\\b") || Regex.IsMatch(text2, "\\bafter\\s+" + Regex.Escape(text3) + "\\s+suffers\\b"))))
		{
			item2 = TargetScope.Self;
		}
		else if (text2.Contains("your target character in this rank") || text2.Contains("in this rank"))
		{
			item2 = TargetScope.SameRank;
		}
		else if (Regex.IsMatch(text2, "\\bwithin\\s+(?:one|1)\\s+rank\\b"))
		{
			item2 = TargetScope.WithinRankDistance;
			item3 = 1;
		}
		else if (Regex.IsMatch(text2, "\\bwithin\\s+(?:two|2)\\s+ranks\\b"))
		{
			item2 = TargetScope.WithinRankDistance;
			item3 = 2;
		}
		else if (text2.Contains("opposing character") || text2.Contains("opponent"))
		{
			item2 = TargetScope.Opposing;
		}
		else if (text2.Contains("your target character") || text2.Contains("character you control") || text2.Contains("friendly"))
		{
			item2 = TargetScope.Friendly;
		}
		return (Timing: item, Scope: item2, MaxDist: item3);
	}

	static RulesParser()
	{
		int num = 39;
		List<EffectMatcher> list = new List<EffectMatcher>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<EffectMatcher> span = CollectionsMarshal.AsSpan(list);
		span[0] = (string text) =>
		{
			if (Regex.IsMatch(text, "Each\\s+player\\s+(?:may\\s+)?draws?\\s+a\\s+card", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "Draw\\s+(\\d+|a)\\s+cards?", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			string value = match.Groups[1].Value;
			return new DrawCardEffect(value.Equals("a", StringComparison.OrdinalIgnoreCase) ? 1 : int.Parse(value));
		};
		span[1] = (string text) =>
		{
			if (Regex.IsMatch(text, "for\\s+each|permanently\\s+gains\\s+\\+1\\s+level", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "(?:gains\\s+)?([+-]?\\d+)\\s+ATK", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				return new ModifyStatEffect("ATK", int.Parse(match.Groups[1].Value));
			}
			Match match2 = Regex.Match(text, "The\\s+strike\\s+gets\\s+([+-]?\\d+)", RegexOptions.IgnoreCase);
			return match2.Success ? new ModifyStatEffect("ATK", int.Parse(match2.Groups[1].Value)) : null;
		};
		span[2] = (string text) =>
		{
			if (Regex.IsMatch(text, "for\\s+each|permanently\\s+gains\\s+\\+1\\s+level", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "([+-]?\\d+)\\s+AC", RegexOptions.IgnoreCase);
			return (!match.Success) ? null : new ModifyStatEffect("CurrentArmorClass", int.Parse(match.Groups[1].Value));
		};
		span[3] = (string text) =>
		{
			if (Regex.IsMatch(text, "for\\s+each|permanently\\s+gains\\s+\\+1\\s+level", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "([+-]?\\d+)\\s+HP", RegexOptions.IgnoreCase);
			return (!match.Success) ? null : new ModifyStatEffect("CurrentHitPoints", int.Parse(match.Groups[1].Value));
		};
		span[4] = (string text) =>
		{
			if (Regex.IsMatch(text, "bonus to ATK and AC equal to this (?:character's|wizard's) level", RegexOptions.IgnoreCase))
			{
				return new ModifyStatEffect("ATK_AND_AC_BY_LEVEL", 1);
			}
			if (Regex.IsMatch(text, "bonus to ATK equal to this (?:character's|wizard's) level", RegexOptions.IgnoreCase))
			{
				return new ModifyStatEffect("ATK_BY_LEVEL", 1);
			}
			if (Regex.IsMatch(text, "bonus to AC equal to this (?:character's|wizard's) level", RegexOptions.IgnoreCase))
			{
				return new ModifyStatEffect("AC_BY_LEVEL", 1);
			}
			return Regex.IsMatch(text, "for each opposing character within one rank", RegexOptions.IgnoreCase) ? new ModifyStatEffect("EVEN_THE_ODDS", 1) : null;
		};
		span[5] = (string text) =>
		{
			if (Regex.IsMatch(text, "\\bTrue Strike\\b", RegexOptions.IgnoreCase) && !text.Contains("ATK", StringComparison.OrdinalIgnoreCase))
			{
				return new GrantTraitEffect("TRUE_STRIKE");
			}
			if (Regex.IsMatch(text, "melee strikes inflict an additional wound this turn", RegexOptions.IgnoreCase))
			{
				return new GrantTraitEffect("MELEE_WOUND_BONUS");
			}
			if (Regex.IsMatch(text, "^Before\\b.*?\\bstrike roll:.*?inflicts an additional wound against higher level", RegexOptions.IgnoreCase))
			{
				return new GrantTraitEffect("CLEAVE_HIGHER_LEVEL_WOUND");
			}
			if (Regex.IsMatch(text, "^Before\\b.*?\\bstrike roll:.*?The strike inflicts an additional wound", RegexOptions.IgnoreCase))
			{
				return new GrantTraitEffect("MELEE_WOUND_BONUS");
			}
			if (Regex.IsMatch(text, "^Before\\b.*?\\bstrike roll:.*?destroy one item equipped", RegexOptions.IgnoreCase))
			{
				return new GrantTraitEffect("CRUSH_DESTROY_ITEM");
			}
			if (Regex.IsMatch(text, "strike that missed:\\s*(?:The attacker suffers a wound|Inflict a wound on the target)", RegexOptions.IgnoreCase))
			{
				return new GrantTraitEffect("POUNCE_MISS_WOUND");
			}
			return Regex.IsMatch(text, "Push the attacker one rank backward", RegexOptions.IgnoreCase) ? new GrantTraitEffect("SHIELD_OF_SPURNING_PUSH") : null;
		};
		span[6] = (string text) =>
		{
			Match match = Regex.Match(text, "additional\\s+(?:melee\\s+)?strike\\s*(?:\\(base\\s*([+-]?\\d+)\\))?", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			int modifier = 0;
			if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var result))
			{
				modifier = result;
			}
			return new GrantAdditionalStrikeEffect(modifier);
		};
		span[7] = (string text) => Regex.IsMatch(text, "permanently gains \\+1 level", RegexOptions.IgnoreCase) ? new PermanentLevelUpEffect() : null;
		span[8] = (string text) =>
		{
			if (Regex.IsMatch(text, "Move the target forward up to two ranks|If (?:this|the) strike hits", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "Move\\s+(?:to\\s+)?rank\\s+(\\d+)", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				return new MoveRankEffect(int.Parse(match.Groups[1].Value));
			}
			Match match2 = Regex.Match(text, "\\bmoves?\\s+(?:.*?\\s+)?(forward|backward)(?:\\s+(\\d+|one|two)\\s+ranks?)?\\b", RegexOptions.IgnoreCase);
			if (match2.Success)
			{
				string value = match2.Groups[2].Value;
				int num2 = 1;
				int result;
				if (value.Equals("two", StringComparison.OrdinalIgnoreCase))
				{
					num2 = 2;
				}
				else if (int.TryParse(value, out result))
				{
					num2 = result;
				}
				bool flag = match2.Groups[1].Value.Equals("forward", StringComparison.OrdinalIgnoreCase);
				int? delta = (flag ? (-num2) : num2);
				return new MoveRankEffect(null, delta);
			}
			return (IEffect?)null;
		};
		span[9] = (string text) =>
		{
			Match match = Regex.Match(text, "Deal\\s+(\\d+)\\s+damage", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				return new DealDamageEffect(int.Parse(match.Groups[1].Value), "magic");
			}
			if (Regex.IsMatch(text, "save\\s*\\(DC|Move the target forward up to two ranks|strike that missed|After the strike,\\s*she suffers a wound", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match2 = Regex.Match(text, "(?:(?:^|:\\s*)inflicts?|\\bsuffers?)\\s+(?:a|an|(\\d+))\\s+wounds?(?!\\s+from)", RegexOptions.IgnoreCase);
			int result;
			return match2.Success ? new DealDamageEffect((!match2.Groups[1].Success || !int.TryParse(match2.Groups[1].Value, out result)) ? 1 : result, "physical") : null;
		};
		span[10] = (string text) =>
		{
			if (Regex.IsMatch(text, "\\bBefore\\b.*?\\b(?:strike roll|performs a melee strike):", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			if (Regex.IsMatch(text, "Target\\s+(?:opposing\\s+player|opponent)\\s+chooses", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			if (Regex.IsMatch(text, "\\bShadow\\s+Bolts\\b|If\\s+(?:the\\s+performer|this\\s+character)\\s+is\\s+a\\s+rogue.*?If\\s+(?:the\\s+performer|this\\s+character)\\s+is\\s+a\\s+wizard", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			if (Regex.IsMatch(text, "target\\s+(?:character\\s+)?performs\\s+a\\s+(?:\\+?\\d+\\s+)?melee\\s+strike|\\bAfter\\b.*?\\bperforms\\s+a\\s+melee\\s+strike|inflicts\\s+an\\s+additional\\s+wound\\s+against\\s+characters\\s+that\\s+are\\s+equal\\s+or\\s+lower", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			if (Regex.IsMatch(text, "Your\\s+two\\s+target\\s+characters\\s+attack|Move the target forward up to two ranks|Banish\\s+the\\s+targets|number of \\+0 ranged strikes equal to this wizard's Level|magic save\\s*\\(DC|dexterity save\\s*\\(DC|unable to perform melee strikes", RegexOptions.IgnoreCase))
			{
				return (IEffect?)null;
			}
			Match match = Regex.Match(text, "(?:Perform|makes?)\\s+(?:a\\s+)?(?:\\+?([+-]?\\d+)\\s+)?(?:(melee|ranged)\\s+)?strike(?:\\s+roll)?", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				match = Regex.Match(text, "Perform\\s+a\\s+(melee|ranged)\\s+strike(?:\\s+with\\s+\\+?([+-]?\\d+)\\s+ATK)?", RegexOptions.IgnoreCase);
			}
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			bool isRanged = match.Groups[0].Value.Contains("ranged", StringComparison.OrdinalIgnoreCase);
			int strikeBonus = 0;
			int result2;
			if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var result))
			{
				strikeBonus = result;
			}
			else if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out result2))
			{
				strikeBonus = result2;
			}
			bool bonusFromLevel = Regex.IsMatch(text, "with a bonus equal to this (?:character's|wizard's) level", RegexOptions.IgnoreCase);
			bool zeroDamage = Regex.IsMatch(text, "does not inflict wounds", RegexOptions.IgnoreCase);
			bool stunOnHit = Regex.IsMatch(text, "(?:If it hits, stun target|stun the target)", RegexOptions.IgnoreCase);
			bool spendOnHit = Regex.IsMatch(text, "(?:If the strike hits, spend the target|spend the target)", RegexOptions.IgnoreCase);
			bool additionalWounds = Regex.IsMatch(text, "(?:inflicts an additional wound|additional wound|\\+1 wound)", RegexOptions.IgnoreCase);
			bool destroyItemOnHit = Regex.IsMatch(text, "destroy one item equipped", RegexOptions.IgnoreCase);
			bool flag = Regex.IsMatch(text, "only target a spent character", RegexOptions.IgnoreCase);
			bool flag2 = Regex.IsMatch(text, "only target a stunned character", RegexOptions.IgnoreCase);
			bool flag3 = Regex.IsMatch(text, "(?:spent or stunned|stunned or spent|against a spent character)", RegexOptions.IgnoreCase);
			bool bonusWoundAgainstHigherLevel = Regex.IsMatch(text, "additional wound against higher level characters", RegexOptions.IgnoreCase);
			bool hasReach = Regex.IsMatch(text, "(?:gains Reach for the strike|target an additional rank away)", RegexOptions.IgnoreCase);
			bool pullOnHit = Regex.IsMatch(text, "move (?:them|the target) forward (?:one|1) rank", RegexOptions.IgnoreCase);
			bool targetLowerLevelOnly = Regex.IsMatch(text, "only target lower level characters", RegexOptions.IgnoreCase);
			bool hasAdvantage = Regex.IsMatch(text, "(?:advantage for the strike|with advantage)", RegexOptions.IgnoreCase);
			bool cannotReduceWounds = Regex.IsMatch(text, "wounds from this action may not be reduced", RegexOptions.IgnoreCase);
			bool banishOnKill = Regex.IsMatch(text, "banish it instead", RegexOptions.IgnoreCase);
			int maxRange = 2;
			if (Regex.IsMatch(text, "three\\s+ranks\\s+away", RegexOptions.IgnoreCase) || (isRanged && Regex.IsMatch(text, "(?:an\\s+)?additional rank away", RegexOptions.IgnoreCase)))
			{
				maxRange = 3;
			}
			else
			{
				Match rangeMatch = Regex.Match(text, "(\\d+)\\s+ranks?\\s+away", RegexOptions.IgnoreCase);
				if (rangeMatch.Success && int.TryParse(rangeMatch.Groups[1].Value, out int parsedRange))
				{
					maxRange = parsedRange;
				}
			}
			bool ignoreRange = Regex.IsMatch(text, "against the target|target character", RegexOptions.IgnoreCase);
			return new PerformStrikeEffect(strikeBonus, isRanged, additionalWounds ? 1 : 0, stunOnHit, spendOnHit, PushOnHit: false, pullOnHit, destroyItemOnHit, zeroDamage, bonusFromLevel, flag && !flag3, flag2 && !flag3, flag3, targetLowerLevelOnly, bonusWoundAgainstHigherLevel, hasReach, TrueStrike: false, hasAdvantage, NoRollBonuses: false, cannotReduceWounds, banishOnKill, maxRange, IgnoreRange: ignoreRange);
		};
		span[11] = (string text) =>
		{
			Match match = Regex.Match(text, "Reduce the wounds.*?by\\s+(\\d+|one)", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			int amount = (match.Groups[1].Value.Equals("one", StringComparison.OrdinalIgnoreCase) ? 1 : int.Parse(match.Groups[1].Value));
			bool woundSelf = Regex.IsMatch(text, "wound\\s+(?:Tyren|this character|himself|herself|itself)", RegexOptions.IgnoreCase);
			return new PreventWoundsEffect(amount, woundSelf);
		};
		span[12] = (string text) => Regex.IsMatch(text, "\\bHeal\\b", RegexOptions.IgnoreCase) ? new HealEffect() : null;
		span[13] = (string text) =>
		{
			if (Regex.IsMatch(text, "\\bUnstun\\b", RegexOptions.IgnoreCase))
			{
				return new ChangeReadyStateEffect(ReadyState.Spent, OnlyIfStunned: true);
			}
			if (Regex.IsMatch(text, "\\bReady target\\b|\\bReady the target\\b|\\bHeal and ready the target\\b|\\bReady (?:her|him|this character)\\b", RegexOptions.IgnoreCase))
			{
				return new ChangeReadyStateEffect(ReadyState.Ready);
			}
			return Regex.IsMatch(text, "\\bJourney\\b", RegexOptions.IgnoreCase) ? new JourneyEffect() : null;
		};
		span[14] = (string text) => Regex.IsMatch(text, "Destroy\\s+(?:the|an|one|target)\\s+item", RegexOptions.IgnoreCase) ? new DestroyTargetItemEffect() : null;
		span[15] = (string text) => Regex.IsMatch(text, "Return\\s+an?\\s+(?:Armor|Shield|Weapon).*?equipped.*?to\\s+(?:its\\s+owner's\\s+hand|hand)", RegexOptions.IgnoreCase) ? new SalvageEquippedItemEffect() : null;
		span[16] = (string text) => Regex.IsMatch(text, "Banish\\s+the\\s+targets.*?\\+3\\s+ranged\\s+strike", RegexOptions.IgnoreCase) ? new BanishFromDiscardAndStrikeEffect() : null;
		span[17] = (string text) => Regex.IsMatch(text, "Reveal\\s+the\\s+top\\s+10\\s+cards", RegexOptions.IgnoreCase) ? new WellSuppliedEffect() : null;
		span[18] = (string text) => Regex.IsMatch(text, "Each\\s+player\\s+draws\\s+a\\s+card.*?gains\\s+\\+2\\s+ATK\\s+and\\s+\\+1\\s+HP", RegexOptions.IgnoreCase) ? new StrongarmStoutEffect() : null;
		span[19] = (string text) =>
		{
			Match match = Regex.Match(text, "fortitude save\\s*\\(DC\\s*(\\d+)\\)\\s*or\\s+suffer\\s+a\\s+wound", RegexOptions.IgnoreCase);
			return match.Success ? new FortitudeSaveEffect(int.Parse(match.Groups[1].Value), PerEquippedItem: text.Contains("each item", StringComparison.OrdinalIgnoreCase), FailureEffects: [new DealDamageEffect(1, "physical")]) : null;
		};
		span[20] = (string text) =>
		{
			Match match = Regex.Match(text, "will save\\s*\\(DC\\s*(\\d+)(?:\\s*\\+\\s*([a-zA-Z\\s']+))?\\)\\s*or\\s+suffer\\s+a\\s+wound", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			int dC = int.Parse(match.Groups[1].Value);
			bool success = match.Groups[2].Success;
			return new WillSaveEffect(dC, [new DealDamageEffect(1, "magic")], success);
		};
		span[21] = (string text) =>
		{
			Match match = Regex.Match(text, "dexterity save\\s*\\(DC\\s*(\\d+)(?:\\s*\\+\\s*([a-zA-Z\\s']+))?\\)\\s*or\\s+be\\s+unable\\s+to\\s+perform\\s+melee\\s+strikes", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return (IEffect?)null;
			}
			int dC = int.Parse(match.Groups[1].Value);
			bool success = match.Groups[2].Success;
			return new ReflexSaveEffect(dC, [new GrantTraitEffect("NO_MELEE_STRIKES")], success);
		};
		span[22] = (string text) => Regex.IsMatch(text, "reveal the top card of your deck.*?bonus to AC", RegexOptions.IgnoreCase) ? new FateshiftEffect() : null;
		span[23] = (string text) => Regex.IsMatch(text, "number of \\+0 ranged strikes equal to this wizard's Level", RegexOptions.IgnoreCase) ? new MagicMissilesEffect() : null;
		span[24] = (string text) => Regex.IsMatch(text, "target performs a melee strike.*?performs a melee strike with advantage", RegexOptions.IgnoreCase) ? new HrunInstigatorEffect() : null;
		span[25] = (string text) => Regex.IsMatch(text, "Move the target forward up to two ranks", RegexOptions.IgnoreCase) ? new TheMightyMuggEffect() : null;
		span[26] = (string text) => Regex.IsMatch(text, "higher level Deverenian Empire character Journeys", RegexOptions.IgnoreCase) ? new TargetHigherLevelDeverenianJourneyEffect() : null;
		span[27] = (string text) => Regex.IsMatch(text, "Move your target character in this rank to a different position in this rank", RegexOptions.IgnoreCase) ? new RepositionInRankEffect() : null;
		span[28] = (string text) => Regex.IsMatch(text, "Put a character into play.*?reduce the character's entering rank by one", RegexOptions.IgnoreCase) ? new PutCharacterIntoPlayEffect() : null;
		span[29] = (string text) => Regex.IsMatch(text, "Redirect the strike to this character", RegexOptions.IgnoreCase) ? new RedirectStrikeEffect() : null;
		span[30] = (string text) => Regex.IsMatch(text, "\\bSurvey\\b", RegexOptions.IgnoreCase) ? new SurveyEffect() : null;
		span[31] = (string text) => Regex.IsMatch(text, "If that character's ATK is lower than.*?stun the target", RegexOptions.IgnoreCase) ? new ChangeReadyStateEffect(ReadyState.Stunned) : null;
		span[32] = (string text) => Regex.IsMatch(text, "Perform a \\+4 ranged strike.*?If\\s+(?:the\\s+performer|this\\s+character)\\s+is\\s+a\\s+rogue", RegexOptions.IgnoreCase) ? new ShadowBoltsEffect() : null;
		span[33] = (string text) => Regex.IsMatch(text, "Target\\s+(?:opposing\\s+player|opponent)\\s+chooses.*?melee\\s+strike", RegexOptions.IgnoreCase) ? new AnotherTavernBrawlEffect() : null;
		span[34] = (string text) => Regex.IsMatch(text, "Your target character performs a melee strike that inflicts an additional wound against characters that are equal or lower level", RegexOptions.IgnoreCase) ? new TargetPerformsStrikeEffect(0, IsRanged: false, BonusWoundAgainstEqualOrLowerLevel: true) : null;
		span[35] = (string text) =>
		{
			Match match = Regex.Match(text, "target performs a \\+?(\\d+) melee strike", RegexOptions.IgnoreCase);
			return match.Success ? new TargetPerformsStrikeEffect(int.Parse(match.Groups[1].Value)) : null;
		};
		span[36] = (string text) => Regex.IsMatch(text, "Your two target characters attack", RegexOptions.IgnoreCase) ? new PackTacticsEffect() : null;
		span[37] = (string text) => Regex.IsMatch(text, "Each player may draw a card.*?draw an additional card", RegexOptions.IgnoreCase) ? new MeetAtTheInnEffect() : null;
		span[38] = (string text) =>
		{
			Match match = Regex.Match(text, "Perform a \\+?(\\d+) ranged strike\\.\\s*After the strike,\\s*any characters who were adjacent to the target must succeed with a magic save\\s*\\(DC\\s*(\\d+)\\)\\s*or suffer a wound", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				return new FireballEffect(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
			}
			return Regex.IsMatch(text, "Fire Resistance", RegexOptions.IgnoreCase) ? new GrantTraitEffect("FIRE_RESISTANCE") : null;
		};
		EffectMatchers = list;
	}
}
