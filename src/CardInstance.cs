using System;
using System.Collections.Generic;

namespace WarlordEngine;

public record struct CardInstance
{
	public required int InstanceId { get; init; }

	public required string DefinitionId { get; init; }

	public int CurrentHitPoints { get; set; }

	public int Wounds { get; set; }

	public int CurrentArmorClass { get; set; }

	public int CurrentSkill { get; set; }

	public int BonusLevel { get; set; }

	public ReadyState ReadyState { get; set; }

	public CardZone Zone { get; set; }

	public int OwnerPlayerIndex { get; set; }

	public int? Rank { get; set; }

	private List<int> _attachedItemIds;

	public List<int> AttachedItemIds
	{
		get
		{
			return _attachedItemIds ?? (_attachedItemIds = new List<int>());
		}
		set
		{
			_attachedItemIds = ((value != null) ? new List<int>(value) : new List<int>());
		}
	}

	public int? AttachedToInstanceId { get; set; }

	private List<string> _statusTags;

	private Dictionary<string, int> _skillRatings;

	private List<int> _abilitiesUsedThisTurn;

	public List<string> StatusTags
	{
		get
		{
			return _statusTags ?? (_statusTags = new List<string>());
		}
		set
		{
			_statusTags = ((value != null) ? new List<string>(value) : new List<string>());
		}
	}

	public Dictionary<string, int> SkillRatings
	{
		get
		{
			return _skillRatings ?? (_skillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
		}
		set
		{
			_skillRatings = ((value != null) ? new Dictionary<string, int>(value, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
		}
	}

	public List<int> AbilitiesUsedThisTurn
	{
		get
		{
			return _abilitiesUsedThisTurn ?? (_abilitiesUsedThisTurn = new List<int>());
		}
		set
		{
			_abilitiesUsedThisTurn = ((value != null) ? new List<int>(value) : new List<int>());
		}
	}

	public CardInstance()
	{
		InstanceId = 0;
		DefinitionId = null;
		CurrentHitPoints = 0;
		Wounds = 0;
		CurrentArmorClass = 0;
		CurrentSkill = 0;
		BonusLevel = 0;
		ReadyState = ReadyState.Ready;
		Zone = CardZone.Deck;
		OwnerPlayerIndex = 0;
		Rank = null;
		AttachedToInstanceId = null;
		_attachedItemIds = new List<int>();
		_statusTags = new List<string>();
		_skillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		_abilitiesUsedThisTurn = new List<int>();
	}

	public CardInstance(CardInstance original)
	{
		_attachedItemIds = new List<int>();
		_statusTags = new List<string>();
		_skillRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		_abilitiesUsedThisTurn = new List<int>();
		this = original;
		_attachedItemIds = ((original._attachedItemIds != null) ? new List<int>(original._attachedItemIds) : new List<int>());
		_statusTags = ((original._statusTags != null) ? new List<string>(original._statusTags) : new List<string>());
		_skillRatings = ((original._skillRatings != null) ? new Dictionary<string, int>(original._skillRatings, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
		_abilitiesUsedThisTurn = ((original._abilitiesUsedThisTurn != null) ? new List<int>(original._abilitiesUsedThisTurn) : new List<int>());
	}

	public readonly CardInstance DeepClone()
	{
		return this with
		{
			AttachedItemIds = ((_attachedItemIds != null) ? new List<int>(_attachedItemIds) : new List<int>()),
			StatusTags = ((_statusTags != null) ? new List<string>(_statusTags) : new List<string>()),
			SkillRatings = ((_skillRatings != null) ? new Dictionary<string, int>(_skillRatings, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)),
			AbilitiesUsedThisTurn = ((_abilitiesUsedThisTurn != null) ? new List<int>(_abilitiesUsedThisTurn) : new List<int>())
		};
	}
}
