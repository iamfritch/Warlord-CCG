using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReadyState
{
	Ready,
	Spent,
	Stunned
}
