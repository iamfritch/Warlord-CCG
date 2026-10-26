using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Events;

public sealed record CharacterDefeatedEvent : GameEvent
{
	public required int DefeatedInstanceId { get; init; }

	public required int OwnerPlayerIndex { get; init; }

	public bool BanishInsteadOfDiscard { get; init; }

	public int? KillerInstanceId { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		PlayerState playerState = state.Players[OwnerPlayerIndex];
		foreach (var (key, list2) in playerState.Ranks)
		{
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].InstanceId == DefeatedInstanceId)
				{
					CardInstance cardInstance = list2[i];
					list2.RemoveAt(i);
					if (BanishInsteadOfDiscard)
					{
						CardInstance item = cardInstance with
						{
							Zone = CardZone.Banished,
							ReadyState = ReadyState.Ready,
							Rank = null,
							Wounds = 0,
							CurrentArmorClass = 0,
							CurrentSkill = 0,
							BonusLevel = 0,
							AttachedItemIds = cardInstance.AttachedItemIds,
							StatusTags = new List<string>(),
							AbilitiesUsedThisTurn = new List<int>()
						};
						playerState.BanishPile.Add(item);
					}
					else
					{
						CardInstance item2 = cardInstance with
						{
							Zone = CardZone.Discard,
							ReadyState = ReadyState.Ready,
							Rank = null,
							Wounds = 0,
							CurrentArmorClass = 0,
							CurrentSkill = 0,
							BonusLevel = 0,
							AttachedItemIds = cardInstance.AttachedItemIds,
							StatusTags = new List<string>(),
							AbilitiesUsedThisTurn = new List<int>()
						};
						playerState.DiscardPile.Add(item2);
					}
					if (list2.Count == 0)
					{
						playerState.Ranks.Remove(key);
					}
					if (playerState.WarlordInstanceId == DefeatedInstanceId)
					{
						playerState.WarlordInstanceId = null;
					}
					BoardGeometry.EnforceFormation(playerState);
					return state;
				}
			}
		}
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private CharacterDefeatedEvent(CharacterDefeatedEvent original)
		: base(original)
	{
		DefeatedInstanceId = original.DefeatedInstanceId;
		OwnerPlayerIndex = original.OwnerPlayerIndex;
		BanishInsteadOfDiscard = original.BanishInsteadOfDiscard;
		KillerInstanceId = original.KillerInstanceId;
	}
}
