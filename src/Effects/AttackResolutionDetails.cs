using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace WarlordEngine.Effects;

public sealed record AttackResolutionDetails
{
	public required int AttackerInstanceId { get; init; }

	public required int DefenderInstanceId { get; init; }

	public string AttackerName { get; init; } = string.Empty;

	public string DefenderName { get; init; } = string.Empty;

	public required int Roll { get; init; }

	public required int StrikeBonus { get; init; }

	public int TotalRoll => Roll + StrikeBonus;

	public required int DefenderArmorClass { get; init; }

	public required bool IsCritical { get; init; }

	public required bool IsCriticalFailure { get; init; }

	public required bool IsHit { get; init; }

	public required int DamageDealt { get; init; }

	public string DamageType { get; init; } = "melee";

	[CompilerGenerated]
	[SetsRequiredMembers]
	private AttackResolutionDetails(AttackResolutionDetails original)
	{
		AttackerInstanceId = original.AttackerInstanceId;
		DefenderInstanceId = original.DefenderInstanceId;
		AttackerName = original.AttackerName;
		DefenderName = original.DefenderName;
		Roll = original.Roll;
		StrikeBonus = original.StrikeBonus;
		DefenderArmorClass = original.DefenderArmorClass;
		IsCritical = original.IsCritical;
		IsCriticalFailure = original.IsCriticalFailure;
		IsHit = original.IsHit;
		DamageDealt = original.DamageDealt;
		DamageType = original.DamageType;
	}
}
