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
		int atk = 0;
		int ac = 0;
		int hp = 0;
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
		int totalBuff = 0;
		foreach (string item in state.TurnState.UsedLimitedAbilityKeysThisTurn)
		{
			if (item.StartsWith(text, StringComparison.OrdinalIgnoreCase) && int.TryParse(item.Substring(text.Length), out var result))
			{
				totalBuff += result;
			}
		}
		return totalBuff;
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
			List<CardInstance> rankCards = state.Players[playerIdx].Ranks[rank];
			CardInstance? leftNeighbor = ((slotIndex > 0) ? new CardInstance?(rankCards[slotIndex - 1]) : ((CardInstance?)null));
			CardInstance? rightNeighbor = ((slotIndex + 1 < rankCards.Count) ? new CardInstance?(rankCards[slotIndex + 1]) : ((CardInstance?)null));
			CardInstance?[] adjacentNeighbors;
			if (definition != null && definition.Name.Equals("Ambitious Squire", StringComparison.OrdinalIgnoreCase))
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition? neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						int neighborLevel = (neighborDef?.Level ?? 0) + (neighbor.Value.SkillRatings.TryGetValue("BonusLevel", out var neighborBonusLevel) ? neighborBonusLevel : 0);
						if (IsDeverenian(neighborDef) && neighborLevel > charLevel)
						{
							atkBonus += 2;
							acBonus += 2;
						}
					}
				}
			}
			if (IsMercenary(definition))
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						if (neighborDef != null && neighborDef.Name.Equals("Balian", StringComparison.OrdinalIgnoreCase))
						{
							acBonus += 2;
							break;
						}
					}
				}
			}
			bool isSelfHazahk = definition != null && definition.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase);
			bool hasAdjacentHazahk = false;
			adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
			for (int i = 0; i < adjacentNeighbors.Length; i++)
			{
				CardInstance? neighbor = adjacentNeighbors[i];
				if (neighbor.HasValue)
				{
					CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
					if (neighborDef != null && neighborDef.Name.Equals("Hazahk of Shadow", StringComparison.OrdinalIgnoreCase))
					{
						hasAdjacentHazahk = true;
						break;
					}
				}
			}
			if ((isSelfHazahk || hasAdjacentHazahk) && (character.Wounds > 0 || isWounded))
			{
				atkBonus += 5;
			}
			if (IsNothrog(definition))
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						if (neighborDef != null && neighborDef.Name.Equals("Joleg", StringComparison.OrdinalIgnoreCase))
						{
							atkBonus += 2;
							break;
						}
					}
				}
			}
			if (IsSiege(definition))
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						if (neighborDef != null && neighborDef.Name.Equals("Makusog Steelhorn", StringComparison.OrdinalIgnoreCase))
						{
							atkBonus += 2;
							break;
						}
					}
				}
			}
			if (charLevel <= 2)
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						if (neighborDef != null && neighborDef.Name.Equals("Shannon the Daring", StringComparison.OrdinalIgnoreCase))
						{
							atkBonus += 2;
							break;
						}
					}
				}
			}
			if (IsDwarf(definition))
			{
				adjacentNeighbors = new CardInstance?[2] { leftNeighbor, rightNeighbor };
				for (int i = 0; i < adjacentNeighbors.Length; i++)
				{
					CardInstance? neighbor = adjacentNeighbors[i];
					if (neighbor.HasValue)
					{
						CardDefinition neighborDef = cardDb.GetDefinition(neighbor.Value.DefinitionId);
						if (neighborDef != null && neighborDef.Name.Equals("Tunnel Vanguard", StringComparison.OrdinalIgnoreCase))
						{
							hpBonus++;
						}
					}
				}
			}
			for (int j = 0; j < rankCards.Count; j++)
			{
				if (j == slotIndex)
				{
					continue;
				}
				foreach (int attachedItemId in rankCards[j].AttachedItemIds)
				{
					CardInstance? equippedItem = CardLocator.FindCard(state, attachedItemId);
					if (equippedItem.HasValue)
					{
						CardDefinition itemDef = cardDb.GetDefinition(equippedItem.Value.DefinitionId);
						if (itemDef != null && itemDef.Name.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase))
						{
							atkBonus += 2;
							acBonus += 2;
						}
					}
				}
			}
			if ((IsDwarf(definition) & isWounded) && rankCards.Any((CardInstance c) =>
			{
				CardDefinition def = cardDb.GetDefinition(c.DefinitionId);
				return def != null && def.Name.Equals("Dress", StringComparison.OrdinalIgnoreCase);
			}))
			{
				acBonus += 3;
			}
			if (IsFreeKingdoms(definition))
			{
				for (int squireIdx = 0; squireIdx < rankCards.Count; squireIdx++)
				{
					if (squireIdx == slotIndex)
					{
						continue;
					}
					CardInstance otherChar = rankCards[squireIdx];
					CardDefinition otherDef = cardDb.GetDefinition(otherChar.DefinitionId);
					if (otherDef != null && otherDef.Name.Equals("Squire Argos", StringComparison.OrdinalIgnoreCase))
					{
						int argosLevel = otherDef.Level + (otherChar.SkillRatings.TryGetValue("BonusLevel", out var argosBonusLevel) ? argosBonusLevel : 0);
						if (charLevel <= argosLevel)
						{
							atkBonus += 2;
							acBonus++;
						}
					}
				}
			}
			if (IsDeverenian(definition) && IsFighter(definition))
			{
				int rearRank = rank + 1;
				if (state.Players[playerIdx].Ranks.TryGetValue(rearRank, out List<CardInstance> rearRankCards) && rearRankCards.Any((CardInstance c) =>
				{
					CardDefinition def = cardDb.GetDefinition(c.DefinitionId);
					return def != null && def.Name.Equals("Taumar the Protector", StringComparison.OrdinalIgnoreCase);
				}))
				{
					acBonus += 2;
					skillBonus += 2;
				}
			}
			for (int oppIdx = 0; oppIdx < state.Players.Count; oppIdx++)
			{
				if (oppIdx == playerIdx)
				{
					continue;
				}
				foreach (var (oppRank, oppRankCards) in state.Players[oppIdx].Ranks)
				{
					if (BoardGeometry.GetCrossBoardDistance(rank, oppRank) > 1)
					{
						continue;
					}
					foreach (CardInstance enemy in oppRankCards)
					{
						CardDefinition enemyDef = cardDb.GetDefinition(enemy.DefinitionId);
						if (enemyDef != null && enemyDef.Name.Equals("Ghed Bane", StringComparison.OrdinalIgnoreCase))
						{
							int targetLevel = GetEffectiveLevel(state, character, cardDb);
							int ghedLevel = GetEffectiveLevel(state, enemy, cardDb);
							if (targetLevel < ghedLevel)
							{
								atkBonus -= 2;
								acBonus -= 2;
							}
						}
					}
				}
			}
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
			if (definition12.Name.Equals("Shillelagh", StringComparison.OrdinalIgnoreCase))
			{
				int currentEffectiveSkill = character.CurrentSkill + skillBonus;
				int shillelaghBonus = Math.Max(0, (currentEffectiveSkill + 1) / 2);
				atkBonus += shillelaghBonus;
			}
			if (definition12.Name.Equals("Gapfinder", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				atkBonus += 5;
			}
			if (definition12.Name.Equals("Scale Mail", StringComparison.OrdinalIgnoreCase) && combatCtx != null && combatCtx.IsDefending && combatCtx.IsRanged)
			{
				acBonus += 2;
			}
			if (definition12.Name.Equals("Ardian Greathawk", StringComparison.OrdinalIgnoreCase))
			{
				skillBonus += 3;
				continue;
			}
			if (definition12.Name.Equals("Unicorn of Thun", StringComparison.OrdinalIgnoreCase))
			{
				skillBonus += 2;
				continue;
			}
			Match match = Regex.Match(definition12.RulesText, "\\+(\\d+)\\s+Skill", RegexOptions.IgnoreCase);
			if (match.Success && int.TryParse(match.Groups[1].Value, out var result))
			{
				skillBonus += result;
			}
		}
		if ((hasDualWieldItem || HasDualWield(definition)) && (object)combatCtx != null && combatCtx.IsMelee && equippedWeaponCount >= 2)
		{
			extraWounds++;
		}
		if (definition != null)
		{
			if (definition.Name.Equals("'Mad' Pat Carrik", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				extraWounds++;
			}
			if (definition.Name.Equals("Blitzer", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee)
			{
				extraWounds++;
			}
			if (definition.Name.Equals("TFK Brigand", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsDefending)
			{
				extraWounds++;
			}
			if (definition.Name.Equals("Grimfey Prowler", StringComparison.OrdinalIgnoreCase) && (object)combatCtx != null && combatCtx.IsAttacking && (object)combatCtx != null && combatCtx.IsMelee && (object)combatCtx != null && combatCtx.OpponentCard?.ReadyState == ReadyState.Spent)
			{
				atkBonus += character.CurrentSkill + skillBonus;
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
				atkBonus += result2;
				break;
			case "AC":
				acBonus += result2;
				break;
			case "SKILL":
				skillBonus += result2;
				break;
			case "HP":
				hpBonus += result2;
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
					extraWounds += result2;
				}
				break;
			case "WOUND_BONUS":
				extraWounds += result2;
				break;
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
			cardDb = _lastActiveCardDb ?? GetActiveCardDb();
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
		return (cardDb.GetDefinition(character.DefinitionId)?.Level ?? 0) + character.BonusLevel + (character.SkillRatings.TryGetValue("BonusLevel", out var bonusLevel) ? bonusLevel : 0);
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
