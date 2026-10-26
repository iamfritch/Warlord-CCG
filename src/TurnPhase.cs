using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TurnPhase
{
	Setup,
	Ready,
	Draw,
	Main,
	Combat,
	End
}
