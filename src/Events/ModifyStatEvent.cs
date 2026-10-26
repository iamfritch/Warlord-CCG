using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using WarlordEngine.Engine;

namespace WarlordEngine.Events;

public sealed record ModifyStatEvent : GameEvent
{
	public required int TargetInstanceId { get; init; }

	public required string StatName { get; init; }

	public required int Amount { get; init; }

	public override GameState ApplyToState(GameState state)
	{
		int amount = Amount;
		string stat = StatName;
		CardLocator.FindAndMutate(state, TargetInstanceId, delegate(ref CardInstance card)
		{
			switch (stat)
			{
			case "CurrentHitPoints":
				card.CurrentHitPoints += amount;
				card.Wounds = Math.Max(0, card.Wounds - amount);
				break;
			case "CurrentArmorClass":
				card.CurrentArmorClass += amount;
				GameEngine.AddTurnBuff(state, TargetInstanceId, "AC", amount);
				break;
			case "CurrentSkill":
				card.CurrentSkill += amount;
				break;
			case "ATK":
				GameEngine.AddTurnBuff(state, TargetInstanceId, "ATK", amount);
				break;
			case "AC":
				GameEngine.AddTurnBuff(state, TargetInstanceId, "AC", amount);
				break;
			}
		});
		return state;
	}

	[CompilerGenerated]
	[SetsRequiredMembers]
	private ModifyStatEvent(ModifyStatEvent original)
		: base(original)
	{
		TargetInstanceId = original.TargetInstanceId;
		StatName = original.StatName;
		Amount = original.Amount;
	}
}
