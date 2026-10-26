using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactTiming
{
	None,
	AfterMove,
	BeforeStrikeRoll,
	AfterTargetedWithStrike,
	AfterAttack,
	BeforeSuffersWounds,
	AfterSuffersWounds,
	AfterStrikeHit,
	BeforeDies,
	Death,
	Enter,
	Kill,
	AfterWizardCastsSpell
}
