namespace K4System.Achievements;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Reads and validates achievements.jsonc.</summary>
internal static class AchievementFile
{
	public const string FileName = "achievements.jsonc";
	private const string DefaultResource = "K4System.Achievements.default-achievements.jsonc";

	private static readonly JsonDocumentOptions DocumentOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static readonly JsonSerializerOptions EntryOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
	};

	public static string DefaultContent
	{
		get
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultResource)
				?? throw new InvalidOperationException($"Embedded resource {DefaultResource} is missing.");
			using StreamReader reader = new(stream);
			return reader.ReadToEnd();
		}
	}

	private static readonly Lazy<HashSet<string>> DefaultIds = new(() => [.. Parse(DefaultContent, requireNames: false).Select(achievement => achievement.Id)]);

	/// <summary>True for keys of the shipped list, whose names come from the language files.</summary>
	public static bool IsDefaultId(string id) => DefaultIds.Value.Contains(id);

	public static IReadOnlyList<AchievementDefinition> Parse(string content) => Parse(content, requireNames: true);

	private static List<AchievementDefinition> Parse(string content, bool requireNames)
	{
		if (string.IsNullOrWhiteSpace(content))
			throw new InvalidDataException("The achievements file is empty.");

		using JsonDocument document = JsonDocument.Parse(content, DocumentOptions);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("The achievements file must contain a JSON object of achievements.");

		List<AchievementDefinition> achievements = [];
		HashSet<string> ids = new(StringComparer.Ordinal);

		foreach (JsonProperty property in document.RootElement.EnumerateObject())
		{
			string id = property.Name.Trim();
			if (id.Length is 0 or > 64)
				throw new InvalidDataException($"Achievement key '{property.Name}' must be 1 to 64 characters.");
			if (!ids.Add(id))
				throw new InvalidDataException($"Achievement key '{id}' is used twice.");

			AchievementDefinition achievement;
			try
			{
				achievement = property.Value.Deserialize<AchievementDefinition>(EntryOptions)
					?? throw new InvalidDataException($"Achievement '{id}' is empty.");
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException($"Achievement '{id}' is invalid: {ex.Message}", ex);
			}

			achievement.Id = id;
			achievement.Name = string.IsNullOrWhiteSpace(achievement.Name) ? null : achievement.Name.Trim();
			achievement.Command = string.IsNullOrWhiteSpace(achievement.Command) ? null : achievement.Command.Trim();

			if (!Enum.IsDefined(achievement.Type))
				throw new InvalidDataException($"Achievement '{id}' has an unknown Type.");
			if (achievement.Goal < 1)
				throw new InvalidDataException($"Achievement '{id}' needs a Goal of 1 or more.");
			if (achievement.Points < 0)
				throw new InvalidDataException($"Achievement '{id}' cannot give negative Points.");
			if (achievement.Name is { Length: > 64 })
				throw new InvalidDataException($"Achievement '{id}': Name can be at most 64 characters.");
			if (requireNames && achievement.Name is null && !IsDefaultId(id))
				throw new InvalidDataException($"Achievement '{id}' needs a \"Name\", because only the shipped achievements have names in the language files.");

			achievements.Add(achievement);
		}

		return achievements;
	}
}
