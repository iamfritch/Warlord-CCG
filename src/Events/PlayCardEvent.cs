using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record PlayCardEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }

	public required int TargetRank { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		PlayerState playerState = state.Players[PlayerIndex];
		int num = -1;
		for (int i = 0; i < playerState.Hand.Count; i++)
		{
			if (playerState.Hand[i].InstanceId == CardInstanceId)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return state;
		}
		CardInstance item = playerState.Hand[num];
		playerState.Hand.RemoveAt(num);
		item.Zone = CardZone.Board;
		if (!playerState.Ranks.TryGetValue(TargetRank, out List<CardInstance> value))
		{
			value = new List<CardInstance>();
			playerState.Ranks[TargetRank] = value;
		}
		value.Add(item);
		BoardGeometry.EnforceFormation(playerState);
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private PlayCardEvent(PlayCardEvent original)
		: base(original)
	{
		PlayerIndex = original.PlayerIndex;
		CardInstanceId = original.CardInstanceId;
		TargetRank = original.TargetRank;
	}
}
