using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Effects;

public sealed record EffectContext
{
	public required int SourceInstanceId { get; init; }

	public int? TargetInstanceId { get; init; }

	public required GameState State { get; init; }

	public required CardDatabase CardDb { get; init; }

	public DiceRoller RollDice { get; init; } = (int min, int max) => Random.Shared.Next(min, max + 1);

	public Action<AttackResolutionDetails>? OnAttackResolved { get; init; }

	public CardInstance? FindCard(int instanceId)
	{
		foreach (PlayerState player in State.Players)
		{
			foreach (CardInstance item in player.Deck)
			{
				if (item.InstanceId == instanceId)
				{
					return item;
				}
			}
			foreach (CardInstance item2 in player.Hand)
			{
				if (item2.InstanceId == instanceId)
				{
					return item2;
				}
			}
			foreach (CardInstance item3 in player.DiscardPile)
			{
				if (item3.InstanceId == instanceId)
				{
					return item3;
				}
			}
			foreach (CardInstance item4 in player.BanishPile)
			{
				if (item4.InstanceId == instanceId)
				{
					return item4;
				}
			}
			foreach (KeyValuePair<int, List<CardInstance>> rank in player.Ranks)
			{
				rank.Deconstruct(out var _, out var value);
				foreach (CardInstance item5 in value)
				{
					if (item5.InstanceId == instanceId)
					{
						return item5;
					}
				}
			}
		}
		return null;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private EffectContext(EffectContext original)
	{
		SourceInstanceId = original.SourceInstanceId;
		TargetInstanceId = original.TargetInstanceId;
		State = original.State;
		CardDb = original.CardDb;
		RollDice = original.RollDice;
		OnAttackResolved = original.OnAttackResolved;
	}
}
