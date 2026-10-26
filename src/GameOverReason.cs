using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameOverReason
{
	None,
	WarlordDefeated,
	DeckOut,
	Concession
}
