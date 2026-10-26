using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Alignment
{
	Good,
	Evil,
	Neutral
}
