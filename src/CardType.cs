using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType
{
	Character,
	Action,
	Item,
	Token
}
