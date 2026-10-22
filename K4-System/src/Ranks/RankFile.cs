namespace K4System.Ranks;

using System.Reflection;
using System.Text.Json;

/// <summary>Reads and validates ranks.jsonc.</summary>
internal static class RankFile
{
	public const string FileName = "ranks.jsonc";
	private const string DefaultResourceName = "K4System.Ranks.default-ranks.jsonc";

	private static readonly JsonDocumentOptions DocumentOptions = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	// Kept lenient to match how earlier versions read this file: property names are
	// case-insensitive and numbers may be written as strings.
	private static readonly JsonSerializerOptions EntryOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static readonly Lazy<string> DefaultContentValue = new(() =>
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DefaultResourceName)
			?? throw new InvalidOperationException($"Embedded resource {DefaultResourceName} is missing.");
		using StreamReader reader = new(stream);
		return reader.ReadToEnd();
	});

	public static string DefaultContent => DefaultContentValue.Value;

	/// <summary>
	/// Parses ranks in file order. A repeated key replaces the earlier entry in place,
	/// which is how earlier versions treated duplicates.
	/// </summary>
	public static List<RankDefinition> Parse(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
			throw new InvalidDataException("The ranks file is empty.");

		using JsonDocument document = JsonDocument.Parse(content, DocumentOptions);

		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("The ranks file must contain a JSON object of ranks.");

		List<RankDefinition> ranks = [];
		Dictionary<string, int> positions = new(StringComparer.Ordinal);

		foreach (JsonProperty property in document.RootElement.EnumerateObject())
		{
			if (string.IsNullOrWhiteSpace(property.Name))
				throw new InvalidDataException("Rank keys cannot be blank.");

			if (property.Value.ValueKind != JsonValueKind.Object)
				throw new InvalidDataException($"Rank '{property.Name}' must be a JSON object.");

			RankDefinition rank;
			try
			{
				rank = property.Value.Deserialize<RankDefinition>(EntryOptions)
					?? throw new InvalidDataException($"Rank '{property.Name}' is empty.");
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException($"Rank '{property.Name}' is invalid: {ex.Message}", ex);
			}

			rank.Key = property.Name;
			rank.Id = 0;

			if (positions.TryGetValue(property.Name, out int existing))
				ranks[existing] = rank;
			else
			{
				positions[property.Name] = ranks.Count;
				ranks.Add(rank);
			}
		}

		Validate(ranks);
		return ranks;
	}

	internal static void Validate(List<RankDefinition> ranks)
	{
		if (ranks.Count == 0)
			throw new InvalidDataException("The ranks file does not contain any ranks.");

		HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
		int defaultRankCount = 0;

		foreach (RankDefinition rank in ranks)
		{
			string key = rank.Key;

			if (string.IsNullOrWhiteSpace(rank.Name) || string.IsNullOrWhiteSpace(rank.Color))
				throw new InvalidDataException($"Rank '{key}' must define a name and color.");

			rank.Name = rank.Name.Trim();
			rank.Color = rank.Color.Trim();

			if (rank.Name.Length > 255)
				throw new InvalidDataException($"Rank '{key}' name cannot exceed 255 characters.");
			if (!names.Add(rank.Name))
				throw new InvalidDataException($"Rank name '{rank.Name}' is duplicated. Rank names must be unique.");
			if (rank.Point < -1)
				throw new InvalidDataException($"Rank '{key}' has point value {rank.Point}. Only the default rank may use a negative value, and it must be -1.");
			if (rank.Point == -1)
				defaultRankCount++;

			if (rank.RequiredPermissions is not null)
				rank.RequiredPermissions = rank.RequiredPermissions
					.Where(requirement => !string.IsNullOrWhiteSpace(requirement))
					.Select(requirement => requirement.Trim())
					.ToList();

			if (rank.Permissions is not null)
			{
				foreach (RankPermission permission in rank.Permissions)
				{
					if (permission is null || string.IsNullOrWhiteSpace(permission.DisplayName) || string.IsNullOrWhiteSpace(permission.PermissionName))
						throw new InvalidDataException($"Rank '{key}' contains an invalid granted permission.");
					if (!permission.PermissionName.Trim().StartsWith("@k4system/", StringComparison.OrdinalIgnoreCase))
						throw new InvalidDataException($"Rank '{key}' granted permission '{permission.PermissionName}' must use a K4-owned @k4system/ flag.");

					permission.DisplayName = permission.DisplayName.Trim();
					permission.PermissionName = permission.PermissionName.Trim();
				}
			}

			if (rank.Rewards is not null && rank.Rewards.Any(reward => reward is null || !reward.IsValidCommand))
				throw new InvalidDataException($"Rank '{key}' contains an invalid reward. Only nonblank command rewards are supported.");
		}

		if (defaultRankCount != 1)
			throw new InvalidDataException($"The ranks file must contain exactly one default rank with Point -1; found {defaultRankCount}.");
	}
}
