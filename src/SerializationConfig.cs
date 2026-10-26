using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarlordEngine;

public static class SerializationConfig
{
	public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { (JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};
}
