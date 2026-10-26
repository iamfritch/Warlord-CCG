using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardZone
{
	Deck,
	Hand,
	Board,
	Discard,
	Banished,
	Attached
}
