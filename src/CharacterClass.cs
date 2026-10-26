using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CharacterClass
{
	Fighter,
	Wizard,
	Rogue,
	Cleric,
	Classless
}
