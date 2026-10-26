using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetScope
{
	Any,
	Self,
	SameRank,
	OtherInSameRank,
	WithinRankDistance,
	Opposing,
	Friendly
}
