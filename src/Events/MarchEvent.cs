using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record MarchEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }

	public required int SourceRank { get; init; }

	public required int DestinationRank { get; init; }

	public int? TargetSlotIndex { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		PlayerState playerState = state.Players[PlayerIndex];
		if (!playerState.Ranks.TryGetValue(SourceRank, out List<CardInstance> value))
		{
			return state;
		}
		int num = -1;
		for (int i = 0; i < value.Count; i++)
		{
			if (value[i].InstanceId == CardInstanceId)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return state;
		}
		CardInstance cardInstance = value[num];
		value.RemoveAt(num);
		if (value.Count == 0 && SourceRank != DestinationRank)
		{
			playerState.Ranks.Remove(SourceRank);
		}
		if (!playerState.Ranks.TryGetValue(DestinationRank, out List<CardInstance> value2))
		{
			value2 = new List<CardInstance>();
			playerState.Ranks[DestinationRank] = value2;
		}
		if (TargetSlotIndex.HasValue)
		{
			int insertIdx = Math.Clamp(TargetSlotIndex.Value, 0, value2.Count);
			value2.Insert(insertIdx, cardInstance with
			{
				Rank = DestinationRank
			});
		}
		else
		{
			value2.Add(cardInstance with
			{
				Rank = DestinationRank
			});
		}
		BoardGeometry.EnforceFormation(playerState);
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private MarchEvent(MarchEvent original)
		: base(original)
	{
		PlayerIndex = original.PlayerIndex;
		CardInstanceId = original.CardInstanceId;
		SourceRank = original.SourceRank;
		DestinationRank = original.DestinationRank;
		TargetSlotIndex = original.TargetSlotIndex;
	}
}
