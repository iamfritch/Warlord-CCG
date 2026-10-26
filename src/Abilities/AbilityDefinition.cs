using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using WarlordEngine.Effects;

namespace WarlordEngine.Abilities;

public sealed record AbilityDefinition
{
	public required AbilityTrigger Trigger { get; init; }

	public bool IsLimited { get; init; }

	public required IReadOnlyList<IEffect> Effects { get; init; }

	public string Description { get; init; } = string.Empty;

	public ReactTiming Timing { get; init; }

	public string? RequiredPerformerClass { get; init; }

	public bool SplitSecond { get; init; }

	public TargetScope TargetScope { get; init; }

	public int? MaxRankDistance { get; init; }

	public bool RequiresSpend
	{
		get
		{
			AbilityTrigger trigger = Trigger;
			if (trigger == AbilityTrigger.SpendOrder || trigger == AbilityTrigger.SpendReact)
			{
				return true;
			}
			return false;
		}
	}

	public bool DestroysSource { get; init; }

	public int DiscardHandCost { get; init; }

	public bool RequiresSpendAdjacentAlly { get; init; }

	public bool RequiresSpendAllyInRank { get; init; }

	public int BanishActionFromDiscardCost { get; init; }

	public bool DiscardsOpponentTopDeck { get; init; }

	public bool StunSelfCost { get; init; }

	public bool IsActive
	{
		get
		{
			AbilityTrigger trigger = Trigger;
			if ((uint)trigger <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsReact
	{
		get
		{
			AbilityTrigger trigger = Trigger;
			if ((uint)(trigger - 2) <= 1u)
			{
				return true;
			}
			return false;
		}
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private AbilityDefinition(AbilityDefinition original)
	{
		Trigger = original.Trigger;
		IsLimited = original.IsLimited;
		Effects = original.Effects;
		Description = original.Description;
		Timing = original.Timing;
		RequiredPerformerClass = original.RequiredPerformerClass;
		SplitSecond = original.SplitSecond;
		TargetScope = original.TargetScope;
		MaxRankDistance = original.MaxRankDistance;
		DestroysSource = original.DestroysSource;
		DiscardHandCost = original.DiscardHandCost;
		RequiresSpendAdjacentAlly = original.RequiresSpendAdjacentAlly;
		RequiresSpendAllyInRank = original.RequiresSpendAllyInRank;
		BanishActionFromDiscardCost = original.BanishActionFromDiscardCost;
		DiscardsOpponentTopDeck = original.DiscardsOpponentTopDeck;
		StunSelfCost = original.StunSelfCost;
	}
}
