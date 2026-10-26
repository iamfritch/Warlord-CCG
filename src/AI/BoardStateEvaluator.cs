using System;
using System.Collections.Generic;
using System.Linq;

namespace WarlordEngine.AI;

public sealed class BoardStateEvaluator : IStateEvaluator
{
	private readonly CardDatabase? _cardDb;

	public CardDatabase? CardDb => _cardDb;

	public BoardStateEvaluator(CardDatabase? cardDb = null)
	{
		_cardDb = cardDb;
	}

	public double Evaluate(GameState state, int maximizingPlayerId)
	{
		ArgumentNullException.ThrowIfNull(state, "state");
		if (state.IsGameOver)
		{
			if (state.WinningPlayerIndex == maximizingPlayerId)
			{
				return 100000.0;
			}
			if (state.WinningPlayerIndex.HasValue)
			{
				return -100000.0;
			}
			return 0.0;
		}
		if (maximizingPlayerId < 0 || maximizingPlayerId >= state.Players.Count)
		{
			return 0.0;
		}
		double num = EvaluatePlayer(state.Players[maximizingPlayerId]);
		double num2 = 0.0;
		for (int i = 0; i < state.Players.Count; i++)
		{
			if (i != maximizingPlayerId)
			{
				num2 += EvaluatePlayer(state.Players[i]);
			}
		}
		return num - num2;
	}

	private double EvaluatePlayer(PlayerState player)
	{
		double num = 0.0;
		int num2 = 0;
		int num3 = 0;
		int? num4 = null;
		int num5 = 0;
		int key;
		List<CardInstance> value;
		foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
		{
			rank.Deconstruct(out key, out value);
			int num6 = key;
			List<CardInstance> list = value;
			int num7 = 0;
			foreach (CardInstance item in list)
			{
				if (item.CurrentHitPoints > 0)
				{
					num7++;
					num2++;
					CardDefinition def = _cardDb?.GetDefinition(item.DefinitionId);
					if (IsWarlordCard(item, player, def))
					{
						num4 = num6;
						num5 = GetCharacterWounds(item, def);
					}
				}
			}
			if (num6 == 1)
			{
				num3 = num7;
			}
		}
		num -= (double)num5 * 150.0;
		if (num4.HasValue)
		{
			if (num4.Value >= 2 && num3 >= 2)
			{
				num += 80.0;
			}
			else if (num4.Value == 1 && num2 > 1)
			{
				num -= 100.0;
			}
		}
		foreach (KeyValuePair<int, List<CardInstance>> rank2 in player.Ranks)
		{
			rank2.Deconstruct(out key, out value);
			foreach (CardInstance item2 in value)
			{
				if (item2.CurrentHitPoints > 0)
				{
					CardDefinition def2 = _cardDb?.GetDefinition(item2.DefinitionId);
					int cardLevel = GetCardLevel(item2, def2);
					int attackBonus = GetAttackBonus(item2, def2);
					int armorClass = GetArmorClass(item2, def2);
					int characterWounds = GetCharacterWounds(item2, def2);
					double num8 = (double)cardLevel * 25.0 + (double)attackBonus * 8.0 + ((double)armorClass - 10.0) * 6.0 - (double)characterWounds * 15.0;
					num += num8 * item2.ReadyState switch
					{
						ReadyState.Ready => 1.0, 
						ReadyState.Spent => 0.75, 
						ReadyState.Stunned => 0.4, 
						_ => 1.0, 
					};
					num += (double)item2.AttachedItemIds.Count * 15.0;
				}
			}
		}
		return num + ((double)player.Hand.Count * 18.0 + (double)player.Deck.Count * 1.0);
	}

	private static int GetCharacterWounds(CardInstance card, CardDefinition? def)
	{
		int num = 0;
		if ((object)def != null && def.BaseHitPoints.HasValue)
		{
			num = Math.Max(0, def.BaseHitPoints.Value - card.CurrentHitPoints);
		}
		if (num == 0 && card.StatusTags.Count > 0)
		{
			foreach (string statusTag in card.StatusTags)
			{
				if (statusTag.StartsWith("wounds:", StringComparison.OrdinalIgnoreCase) && int.TryParse(statusTag.Substring(7), out var result))
				{
					return result;
				}
				if (statusTag.StartsWith("wound:", StringComparison.OrdinalIgnoreCase) && int.TryParse(statusTag.Substring(6), out var result2))
				{
					return result2;
				}
			}
		}
		return num;
	}

	private static int GetAttackBonus(CardInstance card, CardDefinition? def)
	{
		if ((object)def != null && def.Attack?.Strikes.Count > 0)
		{
			return def.Attack.Strikes.Sum((StrikeDescriptor s) => s.Modifier);
		}
		return 0;
	}

	private static int GetCardLevel(CardInstance card, CardDefinition? def)
	{
		if (def != null && def.Level > 0)
		{
			return def.Level + card.BonusLevel;
		}
		if (card.BonusLevel > 0)
		{
			return card.BonusLevel;
		}
		return 1;
	}

	private static int GetArmorClass(CardInstance card, CardDefinition? def)
	{
		if (card.CurrentArmorClass > 0)
		{
			return card.CurrentArmorClass;
		}
		if ((object)def != null && def.ArmorClass.HasValue && def.ArmorClass.Value > 0)
		{
			return def.ArmorClass.Value;
		}
		return 10;
	}

	private static bool IsWarlordCard(CardInstance card, PlayerState player, CardDefinition? def)
	{
		if (player.WarlordInstanceId.HasValue && card.InstanceId == player.WarlordInstanceId.Value)
		{
			return true;
		}
		if (def != null && def.IsWarlord)
		{
			return true;
		}
		return false;
	}
}
