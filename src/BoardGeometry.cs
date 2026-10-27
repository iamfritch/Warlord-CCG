using System;
using System.Collections.Generic;
using System.Linq;

namespace WarlordEngine;

public static class BoardGeometry
{
	public static Action<string>? OnFormationNotice { get; set; }

	public static int GetDistance(int rankA, int rankB)
	{
		return Math.Abs(rankA - rankB);
	}

	public static int GetCrossFormationDistance(int attackerRank, int defenderRank)
	{
		return attackerRank + defenderRank - 1;
	}

	public static int GetCrossBoardDistance(int attackerRank, int defenderRank)
	{
		return GetCrossFormationDistance(attackerRank, defenderRank);
	}

	public static bool IsRangedValid(int attackerRank, int defenderRank, int maxRange = 2)
	{
		int dist = GetCrossFormationDistance(attackerRank, defenderRank);
		return dist >= 2 && dist <= maxRange;
	}

	public static bool IsSameFormationRangedValid(int attackerRank, int allyRank)
	{
		return Math.Abs(attackerRank - allyRank) == 2;
	}

	public static bool IsMeleeValid(CardInstance attacker, CardInstance defender, GameState state)
	{
		if (attacker.OwnerPlayerIndex == defender.OwnerPlayerIndex)
		{
			return false;
		}
		int? num = FindCardRank(state.Players[attacker.OwnerPlayerIndex], attacker.InstanceId);
		int? num2 = FindCardRank(state.Players[defender.OwnerPlayerIndex], defender.InstanceId);
		if (!num.HasValue || !num2.HasValue)
		{
			return false;
		}
		if (num.Value == 1)
		{
			return num2.Value == 1;
		}
		return false;
	}

	public static bool IsRangedValid(CardInstance attacker, CardInstance defender, GameState state, int maxRange = 2)
	{
		if (attacker.OwnerPlayerIndex == defender.OwnerPlayerIndex)
		{
			return false;
		}
		int? num = FindCardRank(state.Players[attacker.OwnerPlayerIndex], attacker.InstanceId);
		int? num2 = FindCardRank(state.Players[defender.OwnerPlayerIndex], defender.InstanceId);
		if (!num.HasValue || !num2.HasValue)
		{
			return false;
		}
		return IsRangedValid(num.Value, num2.Value, maxRange);
	}

	public static List<CardInstance> GetValidRangedTargets(CardInstance attacker, int weaponRange, GameState state)
	{
		List<CardInstance> list = new List<CardInstance>();
		int? num = FindCardRank(state.Players[attacker.OwnerPlayerIndex], attacker.InstanceId);
		if (!num.HasValue)
		{
			return list;
		}
		foreach (PlayerState player in state.Players)
		{
			if (player.PlayerIndex == attacker.OwnerPlayerIndex)
			{
				continue;
			}
			foreach (var (defenderRank, collection) in player.Ranks)
			{
				if (IsRangedValid(num.Value, defenderRank, weaponRange))
				{
					list.AddRange(collection);
				}
			}
		}
		return list;
	}

	public static int? FindCardRank(PlayerState player, int instanceId)
	{
		foreach (var (value, list2) in player.Ranks)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].InstanceId == instanceId)
				{
					return value;
				}
			}
		}
		return null;
	}

	public static int? FindCardRank(GameState state, int playerIndex, int instanceId)
	{
		if (playerIndex < 0 || playerIndex >= state.Players.Count)
		{
			return null;
		}
		return FindCardRank(state.Players[playerIndex], instanceId);
	}

	public static List<CardInstance> GetAdjacentCharacters(PlayerState player, int instanceId)
	{
		List<CardInstance> list = new List<CardInstance>();
		foreach (var (_, list3) in player.Ranks)
		{
			for (int i = 0; i < list3.Count; i++)
			{
				if (list3[i].InstanceId == instanceId)
				{
					if (i > 0)
					{
						list.Add(list3[i - 1]);
					}
					if (i + 1 < list3.Count)
					{
						list.Add(list3[i + 1]);
					}
					return list;
				}
			}
		}
		return list;
	}

	public static bool AreAdjacent(PlayerState player, int instanceId1, int instanceId2)
	{
		return GetAdjacentCharacters(player, instanceId1).Any((CardInstance c) => c.InstanceId == instanceId2);
	}

	public static bool AreAdjacent(GameState state, int instanceId1, int instanceId2)
	{
		foreach (PlayerState player in state.Players)
		{
			if (AreAdjacent(player, instanceId1, instanceId2))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsFormationLegal(PlayerState player)
	{
		if (player.Ranks.Count == 0)
		{
			return true;
		}
		int num = player.Ranks.Keys.Max();
		for (int i = 2; i <= num; i++)
		{
			int num2 = (player.Ranks.TryGetValue(i - 1, out List<CardInstance> value) ? value.Count : 0);
			if ((player.Ranks.TryGetValue(i, out List<CardInstance> value2) ? value2.Count : 0) > num2)
			{
				return false;
			}
		}
		return true;
	}

	public static bool CanDeployToRank(PlayerState player, int targetRank)
	{
		if (targetRank < 1)
		{
			return false;
		}
		if (targetRank == 1)
		{
			return true;
		}
		int num = (player.Ranks.TryGetValue(targetRank - 1, out List<CardInstance> value) ? value.Count : 0);
		if (num == 0)
		{
			return false;
		}
		List<CardInstance> value2;
		return (player.Ranks.TryGetValue(targetRank, out value2) ? value2.Count : 0) + 1 <= num;
	}

	public static void TrimTrailingEmptyRanks(PlayerState player)
	{
		while (player.Ranks.Count > 0)
		{
			int maxRank = player.Ranks.Keys.Max();
			if (!player.Ranks.TryGetValue(maxRank, out var list) || list.Count == 0)
			{
				player.Ranks.Remove(maxRank);
			}
			else
			{
				break;
			}
		}
	}

	public static void CollapseEmptyRanks(PlayerState player)
	{
		ResolveFallingForward(player);
	}

	public static void ResolveIllegalRanks(PlayerState player)
	{
		ResolveFallingForward(player);
	}

	public static void ResolveFallingForward(PlayerState player)
	{
		TrimTrailingEmptyRanks(player);
		while (player.Ranks.Count != 0)
		{
			TrimTrailingEmptyRanks(player);
			if (player.Ranks.Count == 0)
			{
				break;
			}
			int num = player.Ranks.Keys.Max();
			bool flag = false;
			for (int num2 = 2; num2 <= num; num2++)
			{
				int num3 = (player.Ranks.TryGetValue(num2 - 1, out List<CardInstance> value) ? value.Count : 0);
				int num4 = (player.Ranks.TryGetValue(num2, out List<CardInstance> value2) ? value2.Count : 0);
				if (num4 > num3 && value2 != null && num4 > 0)
				{
					CardInstance cardInstance = value2[0];
					value2.RemoveAt(0);
					ReadyState readyState = cardInstance.ReadyState switch
					{
						ReadyState.Ready => ReadyState.Spent, 
						ReadyState.Spent => ReadyState.Stunned, 
						ReadyState.Stunned => ReadyState.Stunned, 
						_ => ReadyState.Spent, 
					};
					if (!player.IsSimulation)
					{
						OnFormationNotice?.Invoke($"[Formation] Character ID {cardInstance.InstanceId} ({cardInstance.DefinitionId}) fell forward from Rank {num2} to Rank {num2 - 1} and transitioned {cardInstance.ReadyState} -> {readyState}!");
					}
					CardInstance item = cardInstance with
					{
						Rank = num2 - 1,
						ReadyState = readyState
					};
					if (!player.Ranks.TryGetValue(num2 - 1, out List<CardInstance> value3))
					{
						value3 = new List<CardInstance>();
						player.Ranks[num2 - 1] = value3;
					}
					value3.Add(item);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				break;
			}
		}
		TrimTrailingEmptyRanks(player);
	}

	public static void EnforceFormation(PlayerState player)
	{
		ResolveFallingForward(player);
	}
}
