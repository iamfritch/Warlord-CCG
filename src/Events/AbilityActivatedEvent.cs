using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using WarlordEngine.Abilities;

namespace WarlordEngine.Events;

public sealed record AbilityActivatedEvent : GameEvent
{
	public required int SourceInstanceId { get; init; }

	public int? PerformerInstanceId { get; init; }

	public required int AbilityIndex { get; init; }

	public required int[] TargetInstanceIds { get; init; }

	public required AbilityDefinition Ability { get; init; }

	public required int OwnerPlayerIndex { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		if (!Ability.RequiresSpend)
		{
			return state;
		}
		PlayerState playerState = state.Players[OwnerPlayerIndex];
		foreach (var (_, list2) in playerState.Ranks)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].InstanceId == SourceInstanceId)
				{
					CardInstance value = list2[i] with
					{
						ReadyState = ReadyState.Spent
					};
					list2[i] = value;
					return state;
				}
			}
		}
		for (int k = 0; k < playerState.AttachedItems.Count; k++)
		{
			if (playerState.AttachedItems[k].InstanceId == SourceInstanceId)
			{
				CardInstance value3 = playerState.AttachedItems[k] with
				{
					ReadyState = ReadyState.Spent
				};
				playerState.AttachedItems[k] = value3;
				return state;
			}
		}
		for (int j = 0; j < playerState.DiscardPile.Count; j++)
		{
			if (playerState.DiscardPile[j].InstanceId == SourceInstanceId)
			{
				CardInstance value2 = playerState.DiscardPile[j] with
				{
					ReadyState = ReadyState.Spent
				};
				playerState.DiscardPile[j] = value2;
				return state;
			}
		}
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private AbilityActivatedEvent(AbilityActivatedEvent original)
		: base(original)
	{
		SourceInstanceId = original.SourceInstanceId;
		PerformerInstanceId = original.PerformerInstanceId;
		AbilityIndex = original.AbilityIndex;
		TargetInstanceIds = original.TargetInstanceIds;
		Ability = original.Ability;
		OwnerPlayerIndex = original.OwnerPlayerIndex;
	}
}
