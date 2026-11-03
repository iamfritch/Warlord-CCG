using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WarlordEngine.Events;

public sealed record PlayCardEvent : GameEvent
{
	public required int PlayerIndex { get; init; }

	public required int CardInstanceId { get; init; }

	public required int TargetRank { get; init; }

	/// <summary>Card database used for the Faction Loyalty check.</summary>
	[JsonIgnore]
	public CardDatabase? CardDb { get; init; }

	/// <summary>When true (effect-driven entry), the Faction Loyalty check is skipped and the card's ready state is preserved.</summary>
	public bool SkipEntryChecks { get; init; }

	/// <summary>Invoked after the character has entered play (card, rank, player index) to trigger enter-reacts.</summary>
	[JsonIgnore]
	public Action<CardInstance, int, int>? OnEntered { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		PlayerState playerState = state.Players[PlayerIndex];
		int handIndex = playerState.Hand.FindIndex((CardInstance c) => c.InstanceId == CardInstanceId);
		if (handIndex < 0)
		{
			return state;
		}
		CardInstance card = playerState.Hand[handIndex];
		playerState.Hand.RemoveAt(handIndex);
		card.Zone = CardZone.Board;
		card.Rank = TargetRank;
		if (!SkipEntryChecks)
		{
			card.ReadyState = ReadyState.Ready;
		}
		CardDefinition? cardDef = CardDb?.GetDefinition(card.DefinitionId);
		if (!SkipEntryChecks && cardDef != null && playerState.WarlordInstanceId.HasValue)
		{
			CardInstance? warlordCard = CardLocator.FindCard(state, playerState.WarlordInstanceId.Value);
			CardDefinition? warlordDef = warlordCard.HasValue ? CardDb!.GetDefinition(warlordCard.Value.DefinitionId) : null;
			if (warlordDef != null)
			{
				string warlordFaction = warlordDef.GetEffectiveFaction();
				string cardFaction = cardDef.GetEffectiveFaction();
				if (!cardFaction.Equals("Mercenary", StringComparison.OrdinalIgnoreCase) && !cardDef.Traits.Contains("Mercenary", StringComparer.OrdinalIgnoreCase) && !cardFaction.Equals(warlordFaction, StringComparison.OrdinalIgnoreCase))
				{
					card.ReadyState = ReadyState.Stunned;
				}
			}
		}
		if (!playerState.Ranks.TryGetValue(TargetRank, out List<CardInstance> rankCards))
		{
			rankCards = new List<CardInstance>();
			playerState.Ranks[TargetRank] = rankCards;
		}
		rankCards.Add(card);
		OnEntered?.Invoke(card, TargetRank, PlayerIndex);
		return state;
	}

}
