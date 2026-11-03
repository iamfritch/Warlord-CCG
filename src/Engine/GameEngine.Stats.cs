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
		int atk = 0;
		int ac = 0;
		int hp = 0;
		string rulesText = itemDef.RulesText;
		if (!string.IsNullOrWhiteSpace(rulesText))
		{
			string[] array = rulesText.Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (string input in array)
			{
				if (Regex.IsMatch(input, @"(?:^|\s)(?:(?:Spend|Destroy|Limited|Spend Limited|Roll)\s+)?(?:Order|React|Death React|Enter React|Kill React)\b", RegexOptions.IgnoreCase)
					|| Regex.IsMatch(input, @"\b(?:Order|React|Spend|Destroy|Discard|Sacrifice)\b", RegexOptions.IgnoreCase)
					|| Regex.IsMatch(input, @"\b(?:if|while|when|during|against|other characters|equal to)\b", RegexOptions.IgnoreCase))
				{
					continue;
				}
				foreach (Match item in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+ATK", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item.Groups[1].Value, out var result))
					{
						atk += result;
					}
				}
				foreach (Match item2 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+AC", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item2.Groups[1].Value, out var result2))
					{
						ac += result2;
					}
				}
				foreach (Match item3 in Regex.Matches(input, "(?:^|[^\\w])\\+(\\d+)\\s+HP", RegexOptions.IgnoreCase))
				{
					if (int.TryParse(item3.Groups[1].Value, out var result3))
					{
						hp += result3;
					}
				}
			}
		}
		if (itemDef.ArmorClass.HasValue)
		{
			ac += itemDef.ArmorClass.Value;
		}
		if (itemDef.Attack?.Strikes.Count > 0)
		{
			atk += itemDef.Attack.Strikes[0].Modifier;
		}
		if (itemDef.BaseHitPoints.HasValue)
		{
			hp += itemDef.BaseHitPoints.Value;
		}
		return (Atk: atk, Ac: ac, Hp: hp);
	}

	public static TurnBuffType ParseBuffType(string buffType)
	{
		return buffType.ToUpperInvariant() switch
		{
			"ATK" or "ATTACK" => TurnBuffType.Attack,
			"RANGED_ATK" or "RANGED_ATTACK" or "RANGEDATTACK" => TurnBuffType.RangedAttack,
			"AC" or "ARMORCLASS" or "ARMOR_CLASS" => TurnBuffType.ArmorClass,
			"HP" or "HITPOINTS" or "HIT_POINTS" => TurnBuffType.HitPoints,
			"SKILL" => TurnBuffType.Skill,
			"INITIATIVE" => TurnBuffType.Initiative,
			"EXTRA_STRIKE" or "ADDITIONAL_STRIKES" or "EXTRA_STRIKES" => TurnBuffType.ExtraStrikes,
			"WOUND_BONUS" => TurnBuffType.WoundBonus,
			"MELEE_WOUND_BONUS" => TurnBuffType.MeleeWoundBonus,
			"TRUE_STRIKE" => TurnBuffType.TrueStrike,
			"REACH" => TurnBuffType.Reach,
			"STEALTH" => TurnBuffType.Stealth,
			"RIPOSTE" => TurnBuffType.Riposte,
			"MAGIC_RESISTANCE" or "MAGICRESISTANCE" => TurnBuffType.MagicResistance,
			"FIRE_RESISTANCE" or "FIRERESISTANCE" => TurnBuffType.FireResistance,
			"NO_MELEE_STRIKES" => TurnBuffType.NoMeleeStrikes,
			"ADVANTAGE" => TurnBuffType.Advantage,
			"DISADVANTAGE" => TurnBuffType.Disadvantage,
			"REDIRECT_TO_ME" => TurnBuffType.RedirectToMe,
			"DUPE_ACTIVE" => TurnBuffType.DupeActive,
			"WARD" => TurnBuffType.Ward,
			"SURVEYED" => TurnBuffType.Surveyed,
			"BANISH_ON_DEATH" => TurnBuffType.BanishOnDeath,
			"WOUNDED_OPPONENT_THIS_TURN" => TurnBuffType.WoundedOpponentThisTurn,
			"STIRGES_BONUS_STRIKE" => TurnBuffType.StirgesBonusStrike,
			"CIANE_SELF_WOUND" => TurnBuffType.CianeSelfWound,
			"CLEAVE_HIGHER_LEVEL_WOUND" => TurnBuffType.CleaveHigherLevelWound,
			"CUNNING_SHOT_PULL" => TurnBuffType.CunningShotPull,
			"CRUSH_DESTROY_ITEM" => TurnBuffType.CrushDestroyItem,
			"POUNCE_MISS_WOUND" => TurnBuffType.PounceMissWound,
			"SHIELD_OF_SPURNING_PUSH" => TurnBuffType.ShieldOfSpurningPush,
			"PIERCE_ARMOR" => TurnBuffType.PierceArmor,
			"DEALS_MAGIC_DAMAGE" => TurnBuffType.DealsMagicDamage,
			"NEGATE_POWERATTACK" => TurnBuffType.NegatePowerattack,
			"IMMUNE_TO_ATTACK_ACTIONS" => TurnBuffType.ImmuneToAttackActions,
			"FORCE_RANGED_HIT" => TurnBuffType.ForceRangedHit,
			_ => Enum.TryParse<TurnBuffType>(buffType.Replace("_", ""), true, out var parsed) ? parsed : TurnBuffType.Other
		};
	}

	public static void AddTurnBuff(GameState state, int instanceId, TurnBuffType buffType, int value = 1, int? sourceInstanceId = null)
	{
		if (!state.TurnState.ActiveTurnBuffs.TryGetValue(instanceId, out var buffs))
		{
			buffs = new List<TurnBuff>();
			state.TurnState.ActiveTurnBuffs[instanceId] = buffs;
		}
		buffs.Add(new TurnBuff(buffType, value, sourceInstanceId));
	}

	public static void AddTurnBuff(GameState state, int instanceId, string buffType, int value = 0)
	{
		AddTurnBuff(state, instanceId, ParseBuffType(buffType), value);
	}

	public static bool HasTurnBuff(GameState state, int instanceId, TurnBuffType buffType)
	{
		if (state.TurnState.ActiveTurnBuffs.TryGetValue(instanceId, out var buffs))
		{
			for (int i = 0; i < buffs.Count; i++)
			{
				if (buffs[i].Type == buffType)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool HasTurnBuff(GameState state, int instanceId, string buffType)
	{
		return HasTurnBuff(state, instanceId, ParseBuffType(buffType));
	}

	public static int GetTurnBuffValue(GameState state, int instanceId, TurnBuffType buffType)
	{
		int totalBuff = 0;
		if (state.TurnState.ActiveTurnBuffs.TryGetValue(instanceId, out var buffs))
		{
			for (int i = 0; i < buffs.Count; i++)
			{
				if (buffs[i].Type == buffType)
				{
					totalBuff += buffs[i].Value;
				}
			}
		}
		return totalBuff;
	}

	public static int GetTurnBuffValue(GameState state, int instanceId, string buffType)
	{
		return GetTurnBuffValue(state, instanceId, ParseBuffType(buffType));
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
		return def.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
			|| def.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsCharacterWounded(CardInstance card, CardDefinition? def, GameState state, CardDatabase? cardDb)
	{
		if (card.Wounds > 0)
		{
			return true;
		}
		int maxHp = def?.BaseHitPoints ?? card.CurrentHitPoints;
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
						maxHp += GetItemPassiveStats(definition).Hp;
					}
				}
			}
		}
		return card.CurrentHitPoints < maxHp;
	}

	public static int GetWounds(GameState state, CardInstance card, CardDatabase? cardDb = null)
	{
		if (card.Wounds > 0)
		{
			return card.Wounds;
		}
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
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
			cardDb = GetActiveCardDb(state);
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int charLevel = (definition?.Level ?? 0) + (character.SkillRatings.TryGetValue("BonusLevel", out var bonusLevel) ? bonusLevel : 0);
		bool isWounded = IsCharacterWounded(character, definition, state, cardDb);
		int atkBonus = 0;
		int acBonus = 0;
		int hpBonus = 0;
		int skillBonus = 0;
		int extraWounds = 0;
		bool trueStrike = false;
		bool hasReach = false;
		List<StrikeDescriptor> list = new List<StrikeDescriptor>();
		(int, int, int)? position = FindCharacterPosition(state, character.InstanceId);
		if (position.HasValue)
		{
			(int playerIdx, int rank, int slotIndex) = position.Value;
			PassiveAccumulator acc = new PassiveAccumulator
			{
				Atk = atkBonus,
				Ac = acBonus,
				Hp = hpBonus,
				Skill = skillBonus,
				ExtraWounds = extraWounds
			};
			PassiveContext passiveCtx = new PassiveContext
			{
				State = state,
				CardDb = cardDb,
				Character = character,
				Definition = definition,
				Combat = combatCtx,
				CharLevel = charLevel,
				IsWounded = isWounded,
				PlayerIndex = playerIdx,
				Rank = rank,
				SlotIndex = slotIndex,
				Acc = acc
			};
			CardScriptRegistry.ApplySelfPassive(passiveCtx);
			CardScriptRegistry.ApplyBoardPassives(passiveCtx);
			atkBonus = acc.Atk;
			acBonus = acc.Ac;
			hpBonus = acc.Hp;
			skillBonus = acc.Skill;
			extraWounds = acc.ExtraWounds;
		}
		int equippedWeaponCount = 0;
		bool hasDualWieldItem = false;
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
			CardInstance? itemCard = CardLocator.FindCard(state, attachedItemId2);
			if (!itemCard.HasValue)
			{
				continue;
			}
			int? actualAttachedTo = itemCard.Value.AttachedToInstanceId ?? itemCard.Value.AttachedToId;
			if (actualAttachedTo.HasValue && actualAttachedTo.Value != character.InstanceId)
			{
				continue;
			}
			CardDefinition definition12 = cardDb.GetDefinition(itemCard.Value.DefinitionId);
			if (!(definition12 != null))
			{
				continue;
			}
			if (IsWeapon(definition12) || HasDualWield(definition12))
			{
				equippedWeaponCount++;
			}
			if (HasDualWield(definition12))
			{
				hasDualWieldItem = true;
			}
			PassiveAccumulator itemAcc = new PassiveAccumulator
			{
				Atk = atkBonus,
				Ac = acBonus,
				Hp = hpBonus,
				Skill = skillBonus,
				ExtraWounds = extraWounds
			};
			PassiveContext itemCtx = new PassiveContext
			{
				State = state,
				CardDb = cardDb,
				Character = character,
				Definition = definition,
				Combat = combatCtx,
				CharLevel = charLevel,
				IsWounded = isWounded,
				PlayerIndex = position?.Item1 ?? character.OwnerPlayerIndex,
				Rank = position?.Item2 ?? character.Rank ?? 1,
				SlotIndex = position?.Item3 ?? 0,
				Acc = itemAcc
			};
			bool handled = CardScriptRegistry.ApplyItemPassive(itemCtx, definition12);
			atkBonus = itemAcc.Atk;
			acBonus = itemAcc.Ac;
			hpBonus = itemAcc.Hp;
			skillBonus = itemAcc.Skill;
			extraWounds = itemAcc.ExtraWounds;
			if (!handled)
			{
				Match match = Regex.Match(definition12.RulesText, "\\+(\\d+)\\s+Skill", RegexOptions.IgnoreCase);
				if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
				{
					skillBonus += result;
				}
			}
		}
		if ((hasDualWieldItem || HasDualWield(definition)) && (object)combatCtx != null && combatCtx.IsMelee && equippedWeaponCount >= 2)
		{
			extraWounds++;
		}
		if (definition != null)
		{
			PassiveAccumulator lateAcc = new PassiveAccumulator
			{
				Atk = atkBonus,
				Ac = acBonus,
				Hp = hpBonus,
				Skill = skillBonus,
				ExtraWounds = extraWounds
			};
			PassiveContext lateCtx = new PassiveContext
			{
				State = state,
				CardDb = cardDb,
				Character = character,
				Definition = definition,
				Combat = combatCtx,
				CharLevel = charLevel,
				IsWounded = isWounded,
				PlayerIndex = position?.Item1 ?? character.OwnerPlayerIndex,
				Rank = position?.Item2 ?? character.Rank ?? 1,
				SlotIndex = position?.Item3 ?? 0,
				Acc = lateAcc
			};
			CardScriptRegistry.ApplyLatePassive(lateCtx);
			atkBonus = lateAcc.Atk;
			acBonus = lateAcc.Ac;
			hpBonus = lateAcc.Hp;
			skillBonus = lateAcc.Skill;
			extraWounds = lateAcc.ExtraWounds;
		}
		if (state.TurnState.ActiveTurnBuffs.TryGetValue(character.InstanceId, out var activeBuffs))
		{
			for (int i = 0; i < activeBuffs.Count; i++)
			{
				TurnBuff buff = activeBuffs[i];
				switch (buff.Type)
				{
				case TurnBuffType.Attack:
					atkBonus += buff.Value;
					break;
				case TurnBuffType.ArmorClass:
					acBonus += buff.Value;
					break;
				case TurnBuffType.Skill:
					skillBonus += buff.Value;
					break;
				case TurnBuffType.HitPoints:
					hpBonus += buff.Value;
					break;
				case TurnBuffType.TrueStrike:
					trueStrike = true;
					break;
				case TurnBuffType.Reach:
					hasReach = true;
					break;
				case TurnBuffType.ExtraStrikes:
					list.Add(new StrikeDescriptor
					{
						Modifier = buff.Value,
						IsRanged = false
					});
					break;
				case TurnBuffType.MeleeWoundBonus:
					if (combatCtx != null && combatCtx.IsMelee)
					{
						extraWounds += buff.Value;
					}
					break;
				case TurnBuffType.WoundBonus:
					extraWounds += buff.Value;
					break;
				}
			}
		}
		if (character.SkillRatings.TryGetValue("BonusAtk", out var bonusAtkRating))
		{
			atkBonus += bonusAtkRating;
		}
		return new PassiveBonuses
		{
			Atk = atkBonus,
			Ac = acBonus,
			Hp = hpBonus,
			Skill = skillBonus,
			AdditionalWounds = extraWounds,
			TrueStrike = trueStrike,
			HasReach = hasReach,
			AdditionalStrikes = list
		};
	}

	public static int GetEffectiveAttack(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		CardDefinition definition = cardDb.GetDefinition(character.DefinitionId);
		int baseAtk = (((object)definition != null && definition.Attack?.Strikes.Count > 0) ? definition.Attack.Strikes[0].Modifier : 0);
		int itemAtk = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? itemCard = CardLocator.FindCard(state, attachedItemId);
			if (itemCard.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(itemCard.Value.DefinitionId);
				itemAtk += GetItemPassiveStats(itemDef).Atk;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return baseAtk + itemAtk + continuousPassiveBonuses.Atk;
	}

	public static int GetEffectiveArmorClass(GameState state, CardInstance character, CombatContext? combatCtx = null, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, combatCtx, cardDb);
		return character.CurrentArmorClass + continuousPassiveBonuses.Ac;
	}

	public static int GetEffectiveCurrentHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentHitPoints + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveMaxHitPoints(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		int baseHp = (cardDb?.GetDefinition(character.DefinitionId))?.BaseHitPoints ?? character.CurrentHitPoints;
		int itemHp = 0;
		foreach (int attachedItemId in character.AttachedItemIds)
		{
			CardInstance? itemCard = CardLocator.FindCard(state, attachedItemId);
			if (itemCard.HasValue)
			{
				CardDefinition itemDef = cardDb?.GetDefinition(itemCard.Value.DefinitionId);
				itemHp += GetItemPassiveStats(itemDef).Hp;
			}
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return baseHp + itemHp + continuousPassiveBonuses.Hp;
	}

	public static int GetEffectiveSkill(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		PassiveBonuses continuousPassiveBonuses = GetContinuousPassiveBonuses(state, character, null, cardDb);
		return character.CurrentSkill + continuousPassiveBonuses.Skill;
	}

	public static int GetEffectiveLevel(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
		}
		return (cardDb.GetDefinition(character.DefinitionId)?.Level ?? 0) + character.BonusLevel + (character.SkillRatings.TryGetValue("BonusLevel", out var bonusLevel) ? bonusLevel : 0);
	}

	public static IReadOnlyList<StrikeDescriptor> GetEffectiveStrikes(GameState state, CardInstance character, CardDatabase? cardDb = null)
	{
		if (cardDb == null)
		{
			cardDb = GetActiveCardDb(state);
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
