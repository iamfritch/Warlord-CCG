using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TurnPhase
{
	Setup,
	Ready,
	Draw,
	Order,
	Main = Order,
	Combat,
	EndOfTurn,
	End = EndOfTurn
}
