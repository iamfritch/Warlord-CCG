using System.Collections.Generic;

namespace WarlordEngine;

public sealed class PlayerState
{
	public required int PlayerIndex { get; init; }

	public string DisplayName { get; set; } = string.Empty;

	public List<CardInstance> Deck { get; set; } = new List<CardInstance>();

	public List<CardInstance> Hand { get; set; } = new List<CardInstance>();

	public List<CardInstance> DiscardPile { get; set; } = new List<CardInstance>();

	public List<CardInstance> BanishPile { get; set; } = new List<CardInstance>();

	public SortedDictionary<int, List<CardInstance>> Ranks { get; set; } = new SortedDictionary<int, List<CardInstance>>();

	public int? WarlordInstanceId { get; set; }

	public bool IsSimulation { get; set; }

	public PlayerState DeepClone()
	{
		return new PlayerState
		{
			PlayerIndex = PlayerIndex,
			DisplayName = DisplayName,
			Deck = CloneCardList(Deck),
			Hand = CloneCardList(Hand),
			DiscardPile = CloneCardList(DiscardPile),
			BanishPile = CloneCardList(BanishPile),
			Ranks = CloneRanks(Ranks),
			WarlordInstanceId = WarlordInstanceId,
			IsSimulation = IsSimulation
		};
	}

	private static List<CardInstance> CloneCardList(List<CardInstance> source)
	{
		List<CardInstance> list = new List<CardInstance>(source.Count);
		for (int i = 0; i < source.Count; i++)
		{
			list.Add(source[i].DeepClone());
		}
		return list;
	}

	private static SortedDictionary<int, List<CardInstance>> CloneRanks(SortedDictionary<int, List<CardInstance>> source)
	{
		SortedDictionary<int, List<CardInstance>> sortedDictionary = new SortedDictionary<int, List<CardInstance>>();
		foreach (var (key, source2) in source)
		{
			sortedDictionary[key] = CloneCardList(source2);
		}
		return sortedDictionary;
	}
}
