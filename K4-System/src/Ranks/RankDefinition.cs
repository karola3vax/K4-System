namespace K4System.Ranks;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>One rank from ranks.jsonc. The JSON property names are part of the file contract.</summary>
public sealed class RankDefinition
{
	/// <summary>The JSON object key. Used for reward identity, never shown to players.</summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>Sequential runtime id of active ranks. The default rank is always -1.</summary>
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Tag { get; set; }

	[JsonConverter(typeof(WholeNumberConverter))]
	public int Point { get; set; }

	/// <summary>Chat color. After loading, color names are replaced with chat color codes.</summary>
	public string Color { get; set; } = string.Empty;

	/// <summary>The color name as written in ranks.jsonc, lowercase, for the Panorama panel.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public string ColorName { get; set; } = "default";

	public List<RankPermission>? Permissions { get; set; }

	public List<string>? RequiredPermissions { get; set; }

	public string? RewardDescription { get; set; }

	public List<RankReward>? Rewards { get; set; }

	public bool IsDefault => Point == -1;

	public bool HasRequirements => RequiredPermissions?.Any(requirement => !string.IsNullOrWhiteSpace(requirement)) == true;
}

public sealed class RankPermission
{
	public string DisplayName { get; set; } = string.Empty;

	public string PermissionName { get; set; } = string.Empty;
}

public sealed class RankReward
{
	public string Type { get; set; } = string.Empty;

	public string Value { get; set; } = string.Empty;

	public bool IsValidCommand => string.Equals(Type, "command", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Value);
}

/// <summary>Reads whole numbers written as 500, 500.0, or "500", as earlier versions accepted.</summary>
internal sealed class WholeNumberConverter : JsonConverter<int>
{
	public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		decimal value = reader.TokenType switch
		{
			JsonTokenType.Number => reader.GetDecimal(),
			JsonTokenType.String when decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed) => parsed,
			_ => throw new JsonException("Expected a whole number.")
		};

		if (value != decimal.Truncate(value) || value < int.MinValue || value > int.MaxValue)
			throw new JsonException($"{value} is not a whole number.");

		return (int)value;
	}

	public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
