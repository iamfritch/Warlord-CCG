namespace WarlordEngine.Engine;

public enum TurnBuffType
{
	Attack,
	RangedAttack,
	ArmorClass,
	HitPoints,
	Skill,
	Initiative,
	ExtraStrikes,
	WoundBonus,
	MeleeWoundBonus,
	PierceArmor,
	DealsMagicDamage,
	NegatePowerattack,
	ImmuneToAttackActions,
	ForceRangedHit,
	// Combat trigger flags
	CunningShotPull,
	CrushDestroyItem,
	PounceMissWound,
	ShieldOfSpurningPush,
	StirgesBonusStrike,
	CianeSelfWound,
	DupeActive,
	CleaveHigherLevelWound,
	Advantage,
	Disadvantage,
	NoMeleeStrikes,
	RedirectToMe,
	Surveyed,
	BanishOnDeath,
	WoundedOpponentThisTurn,
	Ward,
	TrueStrike,
	Reach,
	Stealth,
	Riposte,
	MagicResistance,
	FireResistance,
	Other
}

public readonly record struct TurnBuff(TurnBuffType Type, int Value = 1, int? SourceInstanceId = null);
