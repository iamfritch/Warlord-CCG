using System.Text.Json.Serialization;

namespace WarlordEngine;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AbilityTrigger
{
	Order,
	SpendOrder,
	React,
	SpendReact,
	DeathReact,
	EnterReact
}
