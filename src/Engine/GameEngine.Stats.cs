using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WarlordEngine.Abilities;
using WarlordEngine.Actions;
using WarlordEngine.Effects;
using WarlordEngine.Events;

namespace WarlordEngine.Engine;

public sealed partial class GameEngine
{
	public static (int Atk, int Ac, int Hp) GetItemPassiveStats(CardDefinition? itemDef)
	{
		if (itemDef == null)
		{
			return (Atk: 0, Ac: 0, Hp: 0);
		}
		string text = itemDef.Name.Trim();
		if (text.Equals("Gapfinder", StringComparison.OrdinalIgnoreCase) || text.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase) || text.Equals("Revenger", StringComparison.OrdinalIgnoreCase) || text.Equals("Shillelagh", StringComparison.OrdinalIgnoreCase))
		{
			return (Atk: 0, Ac: 0, Hp: 0);
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		string rulesText = itemDef.RulesText;
		if (!string.IsNullOrWhiteSpace(rulesText))
		{
			string[] array = rulesText.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (string input in array)
			{
				if (Regex.IsMatch(input, "^\\s*(?:(?:Spend|Destroy|Limited|Spend Limited)\\s+)?(?:Order|React|Death React|Enter React)\\b", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "^\\s*(?:Destroy|Discard|Sacrifice)\\s+(?:this\\s+(?:item|card)|[A-Za-z]+)\\b", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "\\b(?:during|while|other characters|against|equal to)\\b", RegexOptions.IgnoreCase))
				{
					continue;
				}
				foreach (Match item in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+ATK", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item.Groups[1].Value, out var result))
					{
						num += result;
					}
				}
				foreach (Match item2 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+AC", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item2.Groups[1].Value, out var result2))
					{
						num2 += result2;
					}
				}
				foreach (Match item3 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+HP", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item3.Groups[1].Value, out var result3))
					{
						num3 += result3;
					}
				}
			}
		}
		if (itemDef.ArmorClass.HasValue)
		{
			num2 += itemDef.ArmorClass.Value;
		}
		if (itemDef.Attack?.Strikes.Count > 0)
		{
			num += itemDef.Attack.Strikes[0].Modifier;
		}
		if (itemDef.BaseHitPoints.HasValue)
		{
			num3 += itemDef.BaseHitPoints.Value;
		}
		return (Atk: num, Ac: num2, Hp: num3);
	}

	public static void AddTurnBuff(GameState state, int instanceId, string buffType, int value = 0)
	{
		state.TurnState.UsedLimitedAbilityKeysThisTurn.Add($"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:{value}");
	}

	public static bool HasTurnBuff(GameState state, int instanceId, string buffType)
	{
		string prefix = $"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:";
		return state.TurnState.UsedLimitedAbilityKeysThisTurn.Any((string k) => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	public static int GetTurnBuffValue(GameState state, int instanceId, string buffType)
	{
		string text = $"BUFF:{instanceId}:{buffType.ToUpperInvariant()}:";
		int num = 0;
		foreach (string item in state.TurnState.UsedLimitedAbilityKeysThisTurn)
		{
			if (item.StartsWith(text, StringComparison.OrdinalIgnoreCase) && int.TryParse(item.Substring(text.Length), out var result))
			{
				num += result;
			}
		}
		return num;
	}

	public static bool IsDeverenian(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Deverenian", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Deverenian", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsMercenary(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Mercenary", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Mercenary", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsNothrog(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Nothrog", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Nothrog", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool ShareFaction(CardDefinition? a, CardDefinition? b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		if (IsDeverenian(a) && IsDeverenian(b))
		{
			return true;
		}
		if (IsNothrog(a) && IsNothrog(b))
		{
			return true;
		}
		if (IsDwarf(a) && IsDwarf(b))
		{
			return true;
		}
		if (IsFreeKingdoms(a) && IsFreeKingdoms(b))
		{
			return true;
		}
		if (IsMercenary(a) && IsMercenary(b))
		{
			return true;
		}
		bool aElf = (a.GetEffectiveFaction()?.Contains("Elf", StringComparison.OrdinalIgnoreCase) ?? false) || a.Traits.Any((string t) => t.Contains("Elf", StringComparison.OrdinalIgnoreCase));
		bool bElf = (b.GetEffectiveFaction()?.Contains("Elf", StringComparison.OrdinalIgnoreCase) ?? false) || b.Traits.Any((string t) => t.Contains("Elf", StringComparison.OrdinalIgnoreCase));
		if (aElf && bElf)
		{
			return true;
		}
		string? factA = a.GetEffectiveFaction() ?? a.Faction;
		string? factB = b.GetEffectiveFaction() ?? b.Faction;
		if (!string.IsNullOrWhiteSpace(factA) && !string.IsNullOrWhiteSpace(factB) && factA.Equals(factB, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return false;
	}

	public static bool IsDwarf(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		string text = def.GetEffectiveFaction() ?? def.Faction ?? "";
		if (!text.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) && !text.Contains("Dwarven", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) || t.Contains("Dwarven", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsFreeKingdoms(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		if (!(def.GetEffectiveFaction() ?? def.Faction ?? "").Contains("Free Kingdoms", StringComparison.OrdinalIgnoreCase))
		{
			return def.Traits.Any((string t) => t.Contains("Free Kingdoms", StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}

	public static bool IsSiege(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Traits.Any((string t) => t.Contains("Siege", StringComparison.OrdinalIgnoreCase)))
			{
				if (def.Class.HasValue)
				{
					return def.Class.Value.ToString().Contains("Siege", StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool IsFighter(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Class.HasValue || !def.Class.Value.ToString().Contains("Fighter", StringComparison.OrdinalIgnoreCase))
			{
				return def.Traits.Any((string t) => t.Contains("Fighter", StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}
		return false;
	}

	public static bool IsWeapon(CardDefinition? def)
	{
		if (def != null)
		{
			if (!def.Traits.Any((string t) => t.Contains("Weapon", StringComparison.OrdinalIgnoreCase)) && !def.RulesText.Contains("Weapon", StringComparison.OrdinalIgnoreCase))
			{
				if (def.Type == CardType.Item)
				{
					return def.Attack != null;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool HasDualWield(CardDefinition? def)
	{
		if (def == null)
		{
			return false;
		}
		return def.Name.Equals("Revenger", StringComparison.OrdinalIgnoreCase)
			|| def.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
			|| def.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsCharacterWounded(CardInstance card, CardDefinition? def, GameState state, CardDatabase? cardDb)
	{
		if (card.Wounds > 0)
		{
			return true;
		}
		int num = def?.BaseHitPoints ?? card.CurrentHitPoints;
		if (cardDb != null)
		{
			foreach (int attachedItemId in card.AttachedItemIds)
			{
				CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
				if (cardInstance.HasValue)
				{
					CardDefinition definition = cardDb.GetDefinition(cardInstance.Value.DefinitionId);
					if (definition != null)
					{
						num += GetItemPassiveStats(definition).Hp;
					}
				}
			}
		}
		return card.CurrentHitPoints < num;
	}

	public static int GetWounds(GameState state, CardInstance card, CardDatabase? cardDb = null)
	{
		if (card.Wounds > 0)
		{
			return card.Wounds;
		}
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int effectiveMaxHitPoints = GetEffectiveMaxHitPoints(state, card, cardDb);
		return Math.Max(0, effectiveMaxHitPoints - card.CurrentHitPoints);
	}

	public PassiveBonuses GetContinuousPassiveBonuses(CardInstance character, CombatContext? combatCtx = null)
	{
		return GetContinuousPassiveBonuses(_state, character, combatCtx, _cardDb);
	}

	public static PassiveBonuses GetContinuousPassiveBonuses(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int num = (definition?.Level ?? 0) + (character.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0);
		bool flag = IsCharacterWounded(character, definition, state, cardDb);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		bool trueStrike = false;
		bool hasReach = false;
		List<StrikeDescriptor> list = new List<StrikeDescriptor>();
		(int, int, int)? tuple = FindCharacterPosition(state, character.InstanceId);
		if (tuple.HasValue)
		{
			(int, int, int) value2 = tuple.Value;
			int item = value2.Item1;
			int item2 = value2.Item2;
			int item3 = value2.Item3;
			List<CardInstance> list2 = state.Players[item].Ranks[item2];
			CardInstance? cardInstance = ((item3 > 0) ? new CardInstance?(list2[item3 - 1]) : ((CardInstance?)null));
			CardInstance? cardInstance2 = ((item3 + 1 < list2.Count) ? new CardInstance?(list2[item3 + 1]) : ((CardInstance?)null));
			int i;
			CardInstance?[] array;
			if (definition != null && definition.Name.Equals("Ambitious Squire", StringComparison.OrdinalIgnoreCase))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance3 = array[i];
					if (cardInstance3.HasValue)
					{
						CardDefinition? definition2 = cardDb.GetDefinition(cardInstance3.Value.DefinitionId);
						int num7 = (definition2?.Level ?? 0) + (cardInstance3.Value.SkillRatings.TryGetValue("BonusLevel", out var value3) ? value3 : 0);
						if (IsDeverenian(definition2) && num7 > num)
							{
								num2 += 2;
							num3 += 2;
						}
					}
				}
			}
			if (IsMercenary(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance4 = array[i];
					if (cardInstance4.HasValue)
					{
						CardDefinition definition3 = cardDb.GetDefinition(cardInstance4.Value.DefinitionId);
						if (definition3 != null && definition3.Name.Equals("Balian", StringComparison.OrdinalIgnoreCase))
						{
							num3 += 2;
							break;
						}
					}
				}
			}
			bool flag2 = definition != null && definition.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase);
			bool flag3 = false;
			array = new CardInstance?[2] { cardInstance, cardInstance2 };
			for (i = 0; i < array.Length; i++)
			{
				CardInstance? cardInstance6 = array[i];
				if (cardInstance6.HasValue)
				{
					CardDefinition definition4 = cardDb.GetDefinition(cardInstance6.Value.DefinitionId);
					if (definition4 != null && definition4.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase))
					{
						flag3 = true;
						break;
					}
				}
			}
			if ((flag2 || flag3) && (character.Wounds > 0 || flag))
			{
				num2 += 5;
			}
			if (IsNothrog(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance7 = array[i];
					if (cardInstance7.HasValue)
					{
						CardDefinition definition5 = cardDb.GetDefinition(cardInstance7.Value.DefinitionId);
						if (definition5 != null && definition5.Name.Equals("Joleg", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (IsSiege(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance8 = array[i];
					if (cardInstance8.HasValue)
					{
						CardDefinition definition6 = cardDb.GetDefinition(cardInstance8.Value.DefinitionId);
						if (definition6 != null && definition6.Name.Equals("Makusog Steelhorn", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (num <= 2)
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance9 = array[i];
					if (cardInstance9.HasValue)
					{
						CardDefinition definition7 = cardDb.GetDefinition(cardInstance9.Value.DefinitionId);
						if (definition7 != null && definition7.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							break;
						}
					}
				}
			}
			if (IsDwarf(definition))
			{
				array = new CardInstance?[2] { cardInstance, cardInstance2 };
				for (i = 0; i < array.Length; i++)
				{
					CardInstance? cardInstance10 = array[i];
					if (cardInstance10.HasValue)
					{
						CardDefinition definition8 = cardDb.GetDefinition(cardInstance10.Value.DefinitionId);
						if (definition8 != null && definition8.Name.Equals("Tunnel Vanguard", StringComparison.OrdinalIgnoreCase))
						{
							num4++;
						}
					}
				}
			}
			for (int j = 0; j < list2.Count; j++)
			{
				if (j == item3)
				{
					continue;
				}
				foreach (int attachedItemId in list2[j].AttachedItemIds)
				{
					CardInstance? cardInstance11 = CardLocator.FindCard(state, attachedItemId);
					if (cardInstance11.HasValue)
					{
						CardDefinition definition9 = cardDb.GetDefinition(cardInstance11.Value.DefinitionId);
						if (definition9 != null && definition9.Name.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase))
							{
								num2 += 2;
							num3 += 2;
						}
					}
				}
			}
			if ((IsDwarf(definition) & flag) && list2.Any((CardInstance c) =>
			{
				CardDefinition definition13 = cardDb.GetDefinition(c.DefinitionId);
				return definition13 != null && definition13.Name.Equals("Dress", StringComparison.OrdinalIgnoreCase);
			}))
			{
				num3 += 3;
			}
			if (IsFreeKingdoms(definition))
			{
				for (int num8 = 0; num8 < list2.Count; num8++)
				{
					if (num8 == item3)
					{
						continue;
					}
					CardInstance cardInstance12 = list2[num8];
					CardDefinition definition10 = cardDb.GetDefinition(cardInstance12.DefinitionId);
					if (definition10 != null && definition10.Name.Equals("Squire Argos", StringComparison.OrdinalIgnoreCase))
					{
						int num9 = definition10.Level + (cardInstance12.SkillRatings.TryGetValue("BonusLevel", out var value4) ? value4 : 0);
						if (num <= num9)
							{
								num2 += 2;
							num3++;
						}
					}
				}
			}
			if (IsDeverenian(definition) && IsFighter(definition))
			{
				int key = item2 + 1;
				if (state.Players[item].Ranks.TryGetValue(key, out List<CardInstance> value5) && value5.Any((CardInstance c) =>
				{
					CardDefinition definition13 = cardDb.GetDefinition(c.DefinitionId);
					return definition13 != null && definition13.Name.Equals("Taumar the Protector", StringComparison.OrdinalIgnoreCase);
				}))
				{
					num3 += 2;
					num5 += 2;
				}
			}
			for (int num10 = 0; num10 < state.Players.Count; num10++)
			{
				if (num10 == item)
				{
					continue;
				}
				foreach (KeyValuePair<int, List<CardInstance>> rank in state.Players[num10].Ranks)
				{
					rank.Deconstruct(out i, out var value6);
					int num11 = i;
					List<CardInstance> list3 = value6;
					if (BoardGeometry.GetCrossBoardDistance(item2, num11) > 1)
					{
						continue;
					}
					foreach (CardInstance item4 in list3)
					{
						CardDefinition definition11 = cardDb.GetDefinition(item4.DefinitionId);
						if (definition11 != null && definition11.Name.Equals("Ghed Bane", StringComparison.OrdinalIgnoreCase))
						{
							int targetLevel = GetEffectiveLevel(state, character, cardDb);
							int ghedLevel = GetEffectiveLevel(state, item4, cardDb);
							if (targetLevel < ghedLevel)
							{
								num2 -= 2;
							num3 -= 2;
							}
					}
					}
				}
			}
		}
		int num13 = 0;
		bool flag5 = false;
		var attachedItemIds = new HashSet<int>(character.AttachedItemIds);
		var charOwner = state.Players.ElementAtOrDefault(character.OwnerPlayerIndex);
		if (charOwner != null)
		{
			foreach (var it in charOwner.AttachedItems)
			{
				if ((it.AttachedToInstanceId ?? it.AttachedToId) == character.InstanceId)
				{
					attachedItemIds.Add(it.InstanceId);
				}
			}
		}
		foreach (int attachedItemId2 in attachedItemIds)
		{
			CardInstance? cardInstance13 = CardLocator.FindCard(state, attachedItemId2);
			if (!cardInstance13.HasValue)
			{
				continue;
			}
			int? actualAttachedTo = cardInstance13.Value.AttachedToInstanceId ?? cardInstance13.Value.AttachedToId;
			if (actualAttachedTo.HasValue && actualAttachedTo.Value != character.InstanceId)
			{
				continue;
			}
			CardDefinition definition12 = cardDb.GetDefinition(cardInstance13.Value.DefinitionId);
			if (!(definition12 != null))
			{
				continue;
			}
			if (IsWeapon(definition12) || HasDualWield(definition12))
			{
				num13++;
			}
			if (HasDualWield(definition12))
			{
				flag5 = true;
			}
			if (definition12.Name.Equals("Shillelagh", StringComparison.OrdinalIgnoreCase))
			{
				int num14 = character.CurrentSkill + num5;
				int num15 = Math.Max(0, (num14 + 1) / 2);
				num2 += num15;
			}
			if (definition12.Name.Equals("Gapfinder", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				num2 += 5;
			}
			if (definition12.Name.Equals("Scale Mail", StringComparison.OrdinalIgnoreCase) && combatCtx != null && combatCtx.IsDefending && combatCtx.IsRanged)
			{
				num3 += 2;
			}
			if (definition12.Name.Equals("Ardian Greathawk", StringComparison.OrdinalIgnoreCase))
			{
				num5 += 3;
				continue;
			}
			if (definition12.Name.Equals("Unicorn of Thun", StringComparison.OrdinalIgnoreCase))
			{
				num5 += 2;
				continue;
			}
			Match match = Regex.Match(definition12.RulesText, "\\+(\\d+)\\s+Skill", RegexOptions.IgnoreCase);
			if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
			{
				num5 += result;
			}
		}
		if ((flag5 || HasDualWield(definition)) && (object)combatCtx != null && combatCtx.IsMelee && num13 >= 2)
		{
			num6++;
		}
		if (definition != null)
		{
			if (definition.Name.Equals("'Mad' Pat Carrik", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				num6++;
			}
			if (definition.Name.Equals("Blitzer", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				num6++;
			}
			if (definition.Name.Equals("TFK Brigand", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsDefending)
			{
				num6++;
			}
			if (definition.Name.Equals("Grimfey Prowler", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				num2 += character.CurrentSkill + num5;
			}
		}
		string text = $"BUFF:{character.InstanceId}:";
		foreach (string item5 in state.TurnState.UsedLimitedAbilityKeysThisTurn)
		{
			if (!item5.StartsWith(text, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string[] array2 = item5.Substring(text.Length).Split(':');
			if (array2.Length < 2 || !int.TryParse(array2[1], out var result2))
			{
				continue;
			}
			switch (array2[0].ToUpperInvariant())
			{
			case "ATK":
				num2 += result2;
				break;
			case "AC":
				num3 += result2;
				break;
			case "SKILL":
				num5 += result2;
				break;
			case "HP":
				num4 += result2;
				break;
			case "TRUE_STRIKE":
				trueStrike = true;
				break;
			case "REACH":
				hasReach = true;
				break;
			case "EXTRA_STRIKE":
				list.Add(new StrikeDescriptor
				{
					Modifier = result2,
					IsRanged = false
				});
				break;
			case "MELEE_WOUND_BONUS":
				if ((object)combatCtx != null && combatCtx.IsMelee)
				{
					num6 += result2;
				}
				break;
			case "WOUND_BONUS":
				num6 += result2;
				break;
			}
		}
		if (character.SkillRatings.TryGetValue("BonusAtk", out var value8))
		{
			num2 += value8;
		}
		return new PassiveBonuses
		{
			Atk = num2,
			Ac = num3,
			Hp = num4,
			Skill = num5,
			AdditionalWounds = num6,
			TrueStrike = trueStrike,
			HasReach = hasReach,
			AdditionalStrikes = list
		};
	}

	public static int GetEffectiveAttack(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int num = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0);
		int num2 = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(cardInstance.Value.DefinitionId);
				num2 += GetItemPassiveStats(itemDef).Atk;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return num + num2 + continuousPassiveBonuses.Atk;
	}

	public static int GetEffectiveArmorClass(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return character.CurrentArmorClass + continuousPassiveBonuses.Ac;
	}

	public static int GetEffectiveCurrentHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentHitPoints + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveMaxHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int num = (cardDb?.GetDefinition(character.DefinitionId))?.BaseHitPoints ?? character.CurrentHitPoints;
		int num2 = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(cardInstance.Value.DefinitionId);
				num2 += GetItemPassiveStats(itemDef).Hp;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return num + num2 + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveSkill(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentSkill + continuousPassiveBonuses.Skill;
	}

	public static int GetEffectiveLevel(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		int value;
		return (cardDb.GetDefinition(character.DefinitionId)?.Level ?? 0) + character.BonusLevel + (character.SkillRatings.TryGetValue("BonusLevel", out value) ? value : 0);
	}

	public static IReadOnlyList<StrikeDescriptor> GetEffectiveStrikes(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		List<StrikeDescriptor> list = new List<StrikeDescriptor>();
		if ((object)definition != null && definition.Attack?.Strikes.Count > 0)
		{
			list.AddRange(definition.Attack.Strikes);
		}
		else
		{
			list.Add(new StrikeDescriptor
			{
				Modifier = 0,
				IsRanged = false
			});
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, new CombatContext
		{
			IsAttacking = true
		}, cardDb);
		if (continuousPassiveBonuses.AdditionalStrikes.Count > 0)
		{
			list.AddRange(continuousPassiveBonuses.AdditionalStrikes);
		}
		return list;
	}

}
