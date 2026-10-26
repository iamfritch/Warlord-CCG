using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using WarlordEngine.Abilities;
using WarlordEngine.Events;
using WarlordEngine.Effects;

namespace WarlordEngine.Actions;

public sealed record PlayCardAction : PlayerAction, IPlayerAction
{
	public required int CardInstanceId { get; init; }

	public int? TargetRank { get; init; }

	public int? TargetCharacterId { get; init; }

	public int? TargetInstanceId
	{
		get => TargetCharacterId;
		init => TargetCharacterId = value;
	}

	public int? PerformerInstanceId { get; init; }

	public int? AbilityIndex { get; init; }

	public int? ChoiceIndex { get; init; }

	public int? DiscardCardInstanceId { get; init; }

	public int? CostCharacterInstanceId { get; init; }

	public int[] CostBanishCardInstanceIds { get; init; } = Array.Empty<int>();

	private static readonly Regex ExplicitComparisonRegex = new Regex("\\b(?:(?:Requires?|Requirements?)\\s*:?\\s*)?([A-Za-z]+(?:\\s+[A-Za-z]+)*?)\\s*(?:>=|=>|&gt;=|≥)\\s*([+-]?\\d+)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex RequiresKeywordRegex = new Regex("\\b(?:Requires?|Requirements?)\\s*:?\\s*([A-Za-z]+(?:\\s+[A-Za-z]+)*?)\\s*(?::|\\+|\\s)\\s*([+-]?\\d+)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly HashSet<string> ExcludedRequirementWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"draw", "deal", "gain", "gains", "level", "rank", "target", "adjacent", "damage", "wound",
		"wounds", "strike", "strikes"
	};

	public static Dictionary<string, int> ExtractSkillRequirements(CardDefinition cardDef)
	{
		ArgumentNullException.ThrowIfNull(cardDef, "cardDef");
		return ExtractSkillRequirements(cardDef.RulesText, cardDef.Traits, cardDef.SkillRequirements);
	}

	public static Dictionary<string, int> ExtractSkillRequirements(string? rulesText, IEnumerable<string>? tags = null, IDictionary<string, int>? existingReqs = null)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (existingReqs != null)
		{
			foreach (var (key, value) in existingReqs)
			{
				dictionary[key] = value;
			}
		}
		ExtractFromText(rulesText, dictionary);
		if (tags != null)
		{
			foreach (string tag in tags)
			{
				ExtractFromText(tag, dictionary);
			}
		}
		return dictionary;
	}

	private static void ExtractFromText(string? text, Dictionary<string, int> reqs)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		foreach (Match item in ExplicitComparisonRegex.Matches(text))
		{
			string text2 = CleanSkillName(item.Groups[1].Value);
			if (!string.IsNullOrWhiteSpace(text2) && !ExcludedRequirementWords.Contains(text2) && int.TryParse(item.Groups[2].Value, out var result))
			{
				reqs[text2] = result;
			}
		}
		foreach (Match item2 in RequiresKeywordRegex.Matches(text))
		{
			string text3 = CleanSkillName(item2.Groups[1].Value);
			if (!string.IsNullOrWhiteSpace(text3) && !ExcludedRequirementWords.Contains(text3) && int.TryParse(item2.Groups[2].Value, out var result2))
			{
				reqs[text3] = result2;
			}
		}
	}

	private static string CleanSkillName(string raw)
	{
		return Regex.Replace(raw, "^(?:Requires?|Requirements?)\\s*:?\\s*", "", RegexOptions.IgnoreCase).Trim();
	}

	public ActionResult Validate(GameState state, CardDatabase cardDb)
	{
		List<string> list = new List<string>();
		if (PlayerIndex < 0 || PlayerIndex >= state.Players.Count)
		{
			list.Add($"Player index {PlayerIndex} is out of range.");
			return ActionResult.Failure(list);
		}
		PlayerState playerState = state.Players[PlayerIndex];
		CardInstance? cardInstance = playerState.Hand.FirstOrDefault((CardInstance c) => c.InstanceId == CardInstanceId);
		if (!cardInstance.HasValue)
		{
			list.Add($"Card instance {CardInstanceId} is not in Player {PlayerIndex}'s hand.");
			return ActionResult.Failure(list);
		}
		CardDefinition definition = cardDb.GetDefinition(cardInstance.Value.DefinitionId);
		if ((object)definition == null)
		{
			list.Add("Card definition '" + cardInstance.Value.DefinitionId + "' not found in card database.");
			return ActionResult.Failure(list);
		}
		bool isUniqueCard = definition.Traits.Any((string t) => t.Equals("Unique", StringComparison.OrdinalIgnoreCase)) || Regex.IsMatch(definition.RulesText ?? "", @"\bUnique\b", RegexOptions.IgnoreCase);
		if (isUniqueCard)
		{
			bool alreadyControlsSameName = false;
			foreach (List<CardInstance> rankList in playerState.Ranks.Values)
			{
				foreach (CardInstance c in rankList)
				{
					CardDefinition def = cardDb.GetDefinition(c.DefinitionId);
					if (def != null && def.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControlsSameName = true;
						break;
					}
				}
				if (alreadyControlsSameName) break;
			}
			if (!alreadyControlsSameName)
			{
				foreach (CardInstance item in playerState.AttachedItems)
				{
					CardDefinition def = cardDb.GetDefinition(item.DefinitionId);
					if (def != null && def.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControlsSameName = true;
						break;
					}
				}
			}
			if (alreadyControlsSameName)
			{
				if (definition.Type == CardType.Item)
				{
					list.Add($"Cannot equip Unique item '{definition.Name}': player already controls a card with the same name in play.");
				}
				else
				{
					list.Add($"Cannot play Unique character '{definition.Name}': player already controls a card with the same name in play.");
				}
			}
		}
		if (definition.Type == CardType.Item)
		{
			if (!TargetCharacterId.HasValue)
			{
				list.Add("Playing an Item card requires a TargetCharacterId.");
			}
			else
			{
				CardInstance? cardInstance2 = null;
				foreach (PlayerState player in state.Players)
				{
					foreach (List<CardInstance> value in player.Ranks.Values)
					{
						foreach (CardInstance item in value)
						{
							if (item.InstanceId == TargetCharacterId.Value)
							{
								cardInstance2 = item;
								break;
							}
						}
						if (cardInstance2.HasValue)
						{
							break;
						}
					}
					if (cardInstance2.HasValue)
					{
						break;
					}
				}
				if (!cardInstance2.HasValue)
				{
					list.Add($"Target character {TargetCharacterId.Value} not found on the board.");
					return ActionResult.Failure(list);
				}
				if (cardInstance2.Value.OwnerPlayerIndex != PlayerIndex)
				{
					list.Add("Cannot attach items to an opponent's character.");
				}
				CardDefinition definition2 = cardDb.GetDefinition(cardInstance2.Value.DefinitionId);
				if ((object)definition2 == null)
				{
					list.Add("Target character definition '" + cardInstance2.Value.DefinitionId + "' not found in database.");
					return ActionResult.Failure(list);
				}
				ActionResult actionResult = ValidateItemEquip(definition, definition2, cardInstance2.Value, state, cardDb);
				if (!actionResult.IsSuccess)
				{
					list.AddRange(actionResult.Errors);
				}
			}
		}
		else if (TargetCharacterId.HasValue && definition.Type != CardType.Action)
		{
			CardInstance? cardInstance3 = null;
			foreach (PlayerState player2 in state.Players)
			{
				foreach (List<CardInstance> value2 in player2.Ranks.Values)
				{
					foreach (CardInstance item2 in value2)
					{
						if (item2.InstanceId == TargetCharacterId.Value)
						{
							cardInstance3 = item2;
							break;
						}
					}
					if (cardInstance3.HasValue)
					{
						break;
					}
				}
				if (cardInstance3.HasValue)
				{
					break;
				}
			}
			if (!cardInstance3.HasValue)
			{
				list.Add($"Target character {TargetCharacterId.Value} not found on the board.");
				return ActionResult.Failure(list);
			}
			CardDefinition definition3 = cardDb.GetDefinition(cardInstance3.Value.DefinitionId);
			if ((object)definition3 == null)
			{
				list.Add("Target character definition '" + cardInstance3.Value.DefinitionId + "' not found in database.");
				return ActionResult.Failure(list);
			}
			ActionResult actionResult2 = ValidateSkillRequirements(definition, definition3, cardInstance3.Value);
			if (!actionResult2.IsSuccess)
			{
				list.AddRange(actionResult2.Errors);
			}
		}
		if (definition.Type == CardType.Action)
		{
			if (PerformerInstanceId.HasValue)
			{
				CardInstance? cardInstance4 = null;
				if (playerState.Ranks != null)
				{
					foreach (List<CardInstance> value3 in playerState.Ranks.Values)
					{
						foreach (CardInstance item3 in value3)
						{
							if (item3.InstanceId == PerformerInstanceId.Value)
							{
								cardInstance4 = item3;
								break;
							}
						}
						if (cardInstance4.HasValue)
						{
							break;
						}
					}
				}
				if (!cardInstance4.HasValue)
				{
					list.Add($"Performer character {PerformerInstanceId.Value} not found on Player {PlayerIndex}'s board.");
				}
				else
				{
					CardDefinition definition4 = cardDb.GetDefinition(cardInstance4.Value.DefinitionId);
					if ((object)definition4 == null)
					{
						list.Add("Performer card definition '" + cardInstance4.Value.DefinitionId + "' not found in database.");
					}
					else
					{
						ActionResult actionResult3 = ValidatePerformerRequirements(definition, definition4, cardInstance4.Value);
						if (!actionResult3.IsSuccess)
						{
							list.AddRange(actionResult3.Errors);
						}
					}
				}
			}
			else if ((playerState.Ranks?.Values.Sum((List<CardInstance> r) => r.Count) ?? 0) > 0)
			{
				bool flag = false;
				foreach (List<CardInstance> value4 in playerState.Ranks.Values)
				{
					foreach (CardInstance item4 in value4)
					{
						CardDefinition definition5 = cardDb.GetDefinition(item4.DefinitionId);
						if (definition5 != null && ValidatePerformerRequirements(definition, definition5, item4).IsSuccess)
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
				if (!flag)
				{
					list.Add("No valid performer on the board meets the requirements to play action '" + definition.Name + "'.");
				}
			}
			if (PerformerInstanceId.HasValue && TargetCharacterId.HasValue)
			{
				CardInstance? perf = CardLocator.FindCard(state, PerformerInstanceId.Value);
				CardInstance? targ = CardLocator.FindCard(state, TargetCharacterId.Value);
				if (perf.HasValue && targ.HasValue && perf.Value.Rank.HasValue && targ.Value.Rank.HasValue && targ.Value.OwnerPlayerIndex != PlayerIndex)
				{
					int numAbil = AbilityIndex ?? ChoiceIndex.GetValueOrDefault();
					PerformStrikeEffect? strikeEff = null;
					if (numAbil >= 0 && numAbil < definition.Abilities.Count)
					{
						strikeEff = definition.Abilities[numAbil].Effects.OfType<PerformStrikeEffect>().FirstOrDefault();
					}
					else
					{
						strikeEff = definition.Abilities.SelectMany((AbilityDefinition a) => a.Effects).OfType<PerformStrikeEffect>().FirstOrDefault();
					}
					if (strikeEff != null)
					{
						int crossDist = BoardGeometry.GetCrossBoardDistance(perf.Value.Rank.Value, targ.Value.Rank.Value);
						if (strikeEff.IsRanged)
						{
							if (crossDist > strikeEff.MaxRange)
							{
								list.Add($"Target character {TargetCharacterId.Value} is out of range ({crossDist} > {strikeEff.MaxRange}).");
							}
						}
						else
						{
							int maxMelee = strikeEff.HasReach ? 2 : 1;
							if (crossDist > maxMelee)
							{
								list.Add($"Target character {TargetCharacterId.Value} is out of melee range ({crossDist} > {maxMelee}).");
							}
						}
					}
				}
			}
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	public static List<string> GetItemEquipmentSlots(CardDefinition itemDef)
	{
		List<string> list = new List<string>();
		if (itemDef.Traits.Any((string t) => t.Equals("Weapon", StringComparison.OrdinalIgnoreCase) || t.Equals("Sword", StringComparison.OrdinalIgnoreCase) || t.Equals("Axe", StringComparison.OrdinalIgnoreCase) || t.Equals("Bow", StringComparison.OrdinalIgnoreCase) || t.Equals("Crossbow", StringComparison.OrdinalIgnoreCase) || t.Equals("Dagger", StringComparison.OrdinalIgnoreCase) || t.Equals("Polearm", StringComparison.OrdinalIgnoreCase) || t.Equals("Mace", StringComparison.OrdinalIgnoreCase) || t.Equals("Staff", StringComparison.OrdinalIgnoreCase) || t.Equals("Flail", StringComparison.OrdinalIgnoreCase) || t.Equals("Spear", StringComparison.OrdinalIgnoreCase)))
		{
			list.Add("Weapon");
		}
		if (itemDef.Traits.Any((string t) => t.Equals("Armor", StringComparison.OrdinalIgnoreCase) || t.Equals("Plate", StringComparison.OrdinalIgnoreCase) || t.Equals("Mail", StringComparison.OrdinalIgnoreCase) || t.Equals("Leather", StringComparison.OrdinalIgnoreCase)) || itemDef.Name.Equals("Scale Mail", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Equals("Breastplate of Power", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Equals("Breastplate", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Equals("Aroch Mail", StringComparison.OrdinalIgnoreCase))
		{
			list.Add("Armor");
		}
		if (itemDef.Traits.Any((string t) => t.Equals("Shield", StringComparison.OrdinalIgnoreCase) || t.Equals("Buckler", StringComparison.OrdinalIgnoreCase) || t.Equals("Aegis", StringComparison.OrdinalIgnoreCase)) || itemDef.Name.Contains("Shield", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Buckler", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Aegis", StringComparison.OrdinalIgnoreCase))
		{
			list.Add("Shield");
		}
		if (itemDef.Traits.Any((string t) => t.Equals("Helm", StringComparison.OrdinalIgnoreCase) || t.Equals("Helmet", StringComparison.OrdinalIgnoreCase) || t.Equals("Diadem", StringComparison.OrdinalIgnoreCase) || t.Equals("Crown", StringComparison.OrdinalIgnoreCase)) || itemDef.Name.Contains("Helm", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Diadem", StringComparison.OrdinalIgnoreCase))
		{
			list.Add("Helm");
		}
		if (itemDef.Traits.Any((string t) => t.Equals("Steed", StringComparison.OrdinalIgnoreCase) || t.Equals("Mount", StringComparison.OrdinalIgnoreCase) || t.Equals("Horse", StringComparison.OrdinalIgnoreCase)) || itemDef.Name.Contains("Horse", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Mare", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Pony", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Nag", StringComparison.OrdinalIgnoreCase) || itemDef.Name.Contains("Bloodsteed", StringComparison.OrdinalIgnoreCase))
		{
			list.Add("Steed");
		}
		return list;
	}

	public static bool MatchesClass(CardDefinition actionDef, CardDefinition performerDef)
	{
		ArgumentNullException.ThrowIfNull(actionDef, "actionDef");
		ArgumentNullException.ThrowIfNull(performerDef, "performerDef");
		if (!actionDef.Class.HasValue || actionDef.Class == CharacterClass.Classless)
		{
			return true;
		}
		if (performerDef.Class.HasValue)
		{
			if (performerDef.Class == actionDef.Class)
			{
				return true;
			}
			if (actionDef.Traits.Any((string t) => t.Equals($"MultiClass:{performerDef.Class}", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}
		if (actionDef.Class.HasValue && performerDef.Traits.Any((string t) => t.Equals($"MultiClass:{actionDef.Class}", StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		return false;
	}

	public static ActionResult ValidateItemEquip(CardDefinition itemDef, CardDefinition targetDef, CardInstance targetCard, GameState state, CardDatabase cardDb)
	{
		ArgumentNullException.ThrowIfNull(itemDef, "itemDef");
		ArgumentNullException.ThrowIfNull(targetDef, "targetDef");
		List<string> list = new List<string>();
		int currentRank = targetCard.Rank ?? (state != null && targetCard.OwnerPlayerIndex >= 0 && targetCard.OwnerPlayerIndex < state.Players.Count ? BoardGeometry.FindCardRank(state.Players[targetCard.OwnerPlayerIndex], targetCard.InstanceId) : null) ?? 0;
		int num = targetDef.Level + targetCard.BonusLevel + (targetCard.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0) + currentRank;
		if (num < itemDef.Level)
		{
			list.Add($"Target character '{targetDef.Name}' (Level {num}) does not meet required Level {itemDef.Level} for Item '{itemDef.Name}'.");
		}
		if (!MatchesClass(itemDef, targetDef))
		{
			list.Add($"Target character '{targetDef.Name}' ({targetDef.Class}) does not match Item class ({itemDef.Class}).");
		}
		bool isUniqueItem = itemDef.Traits.Any((string t) => t.Equals("Unique", StringComparison.OrdinalIgnoreCase)) || Regex.IsMatch(itemDef.RulesText ?? "", @"\bUnique\b", RegexOptions.IgnoreCase);
		if (isUniqueItem && state != null && targetCard.OwnerPlayerIndex >= 0 && targetCard.OwnerPlayerIndex < state.Players.Count)
		{
			PlayerState ownerPlayer = state.Players[targetCard.OwnerPlayerIndex];
			bool alreadyControls = false;
			foreach (List<CardInstance> rankList in ownerPlayer.Ranks.Values)
			{
				foreach (CardInstance c in rankList)
				{
					CardDefinition def = cardDb.GetDefinition(c.DefinitionId);
					if (def != null && def.Name.Equals(itemDef.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControls = true;
						break;
					}
				}
				if (alreadyControls) break;
			}
			if (!alreadyControls)
			{
				foreach (CardInstance item in ownerPlayer.AttachedItems)
				{
					CardDefinition def = cardDb.GetDefinition(item.DefinitionId);
					if (def != null && def.Name.Equals(itemDef.Name, StringComparison.OrdinalIgnoreCase))
					{
						alreadyControls = true;
						break;
					}
				}
			}
			if (alreadyControls)
			{
				list.Add($"Cannot equip Unique item '{itemDef.Name}': player already controls a card with the same name in play.");
			}
		}
		foreach (int attachedItemId in targetCard.AttachedItemIds)
		{
			CardInstance? cardInstance = CardLocator.FindCard(state, attachedItemId);
			if (cardInstance.HasValue)
			{
				CardDefinition definition = cardDb.GetDefinition(cardInstance.Value.DefinitionId);
				if (definition != null && (definition.DefinitionId == itemDef.DefinitionId || definition.Name.Equals(itemDef.Name, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add($"Target character '{targetDef.Name}' already has '{itemDef.Name}' equipped.");
					break;
				}
			}
		}
		List<string> itemEquipmentSlots = GetItemEquipmentSlots(itemDef);
		if (itemEquipmentSlots.Count > 0)
		{
			foreach (int attachedItemId2 in targetCard.AttachedItemIds)
			{
				CardInstance? cardInstance2 = CardLocator.FindCard(state, attachedItemId2);
				if (!cardInstance2.HasValue)
				{
					continue;
				}
				CardDefinition definition2 = cardDb.GetDefinition(cardInstance2.Value.DefinitionId);
				if (!(definition2 != null))
				{
					continue;
				}
				List<string> itemEquipmentSlots2 = GetItemEquipmentSlots(definition2);
				foreach (string item in itemEquipmentSlots)
				{
					if (itemEquipmentSlots2.Contains(item))
					{
						if (item.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
						{
							bool hasDualWield = targetDef.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
								|| targetDef.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase)
								|| itemDef.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
								|| itemDef.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase)
								|| definition2.Traits.Any((string t) => t.Equals("Dual Wield", StringComparison.OrdinalIgnoreCase))
								|| definition2.RulesText.Contains("Dual Wield", StringComparison.OrdinalIgnoreCase)
								|| itemDef.Name.Equals("Revenger", StringComparison.OrdinalIgnoreCase)
								|| definition2.Name.Equals("Revenger", StringComparison.OrdinalIgnoreCase);
							if (hasDualWield)
							{
								int weaponCount = 0;
								foreach (int existingId in targetCard.AttachedItemIds)
								{
									CardInstance? existingCard = CardLocator.FindCard(state, existingId);
									if (existingCard.HasValue)
									{
										CardDefinition existingDef = cardDb.GetDefinition(existingCard.Value.DefinitionId);
										if (existingDef != null && GetItemEquipmentSlots(existingDef).Contains("Weapon"))
										{
											weaponCount++;
										}
									}
								}
								if (weaponCount < 2)
								{
									continue;
								}
							}
						}
						if (item.Equals("Armor", StringComparison.OrdinalIgnoreCase))
						{
							list.Add($"Target character '{targetDef.Name}' already has armor equipped ('{definition2.Name}').");
						}
						else
						{
							list.Add($"Target character '{targetDef.Name}' already has a {item} equipped ('{definition2.Name}').");
						}
						break;
					}
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(itemDef.RulesText))
		{
			Match match = Regex.Match(itemDef.RulesText, "\\b([A-Za-z]+)\\s+only\\b", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				string req = match.Groups[1].Value;
				if (!targetDef.Traits.Any((string t) => t.Equals(req, StringComparison.OrdinalIgnoreCase)) && (!targetDef.Class.HasValue || !targetDef.Class.Value.ToString().Equals(req, StringComparison.OrdinalIgnoreCase)) && !targetDef.Name.Contains(req, StringComparison.OrdinalIgnoreCase))
				{
					list.Add($"Item '{itemDef.Name}' requires '{req}' restriction.");
				}
			}
		}
		ActionResult actionResult = ValidateSkillRequirements(itemDef, targetDef, targetCard);
		if (!actionResult.IsSuccess)
		{
			list.AddRange(actionResult.Errors);
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	public static ActionResult ValidatePerformerRequirements(CardDefinition actionDef, CardDefinition performerDef, CardInstance performerCard)
	{
		ArgumentNullException.ThrowIfNull(actionDef, "actionDef");
		ArgumentNullException.ThrowIfNull(performerDef, "performerDef");
		List<string> list = new List<string>();
		if (performerCard.ReadyState == ReadyState.Stunned)
		{
			list.Add("Performer '" + performerDef.Name + "' is Stunned and cannot perform actions.");
		}
		if (!MatchesClass(actionDef, performerDef))
		{
			list.Add($"Performer '{performerDef.Name}' ({performerDef.Class}) does not match Action class ({actionDef.Class}).");
		}
		int num = performerDef.Level + performerCard.BonusLevel + (performerCard.SkillRatings.TryGetValue("BonusLevel", out var value) ? value : 0);
		if (num < actionDef.Level)
		{
			list.Add($"Performer '{performerDef.Name}' (Level {num}) does not meet required Level {actionDef.Level} for '{actionDef.Name}'.");
		}
		if ((actionDef.Abilities.Any((AbilityDefinition a) => a.RequiresSpend) || Regex.IsMatch(actionDef.RulesText, "\\bSpend\\s+(?:Limited\\s+)?(?:Order|React):", RegexOptions.IgnoreCase)) && performerCard.ReadyState != ReadyState.Ready)
		{
			list.Add("Performer '" + performerDef.Name + "' must be Ready to perform an action requiring Spend.");
		}
		ActionResult actionResult = ValidateSkillRequirements(actionDef, performerDef, performerCard);
		if (!actionResult.IsSuccess)
		{
			list.AddRange(actionResult.Errors);
		}
		if (list.Count <= 0)
		{
			return ActionResult.Success();
		}
		return ActionResult.Failure(list);
	}

	public ActionResult Validate(CardDefinition cardToPlay, CardDefinition targetCharacterDef, CardInstance? targetCharacterInstance = null)
	{
		return ValidateSkillRequirements(cardToPlay, targetCharacterDef, targetCharacterInstance);
	}

	public static ActionResult ValidateSkillRequirements(CardDefinition cardToPlay, CardDefinition targetCharacterDef, CardInstance? targetCharacterInstance = null)
	{
		ArgumentNullException.ThrowIfNull(cardToPlay, "cardToPlay");
		ArgumentNullException.ThrowIfNull(targetCharacterDef, "targetCharacterDef");
		List<string> list = new List<string>();
		foreach (var (text2, num2) in ExtractSkillRequirements(cardToPlay))
		{
			int num3 = 0;
			int value2;
			if (targetCharacterInstance.HasValue && targetCharacterInstance.Value.SkillRatings.TryGetValue(text2, out var value))
			{
				num3 = value;
			}
			else if (targetCharacterDef.SkillRatings.TryGetValue(text2, out value2))
			{
				num3 = value2;
			}
			if (num3 < num2)
			{
				list.Add($"Target character '{targetCharacterDef.Name}' lacks required {text2} rating of {num2} (current rating: {num3}).");
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
	private PlayCardAction(PlayCardAction original)
		: base(original)
	{
		CardInstanceId = original.CardInstanceId;
		TargetRank = original.TargetRank;
		TargetCharacterId = original.TargetCharacterId;
		PerformerInstanceId = original.PerformerInstanceId;
		AbilityIndex = original.AbilityIndex;
		ChoiceIndex = original.ChoiceIndex;
		DiscardCardInstanceId = original.DiscardCardInstanceId;
		CostCharacterInstanceId = original.CostCharacterInstanceId;
		CostBanishCardInstanceIds = original.CostBanishCardInstanceIds;
	}
}
