namespace K4System.Missions;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>The validated mission list, grouped by difficulty in file order.</summary>
public sealed class MissionPool
{
	public MissionPool(IReadOnlyList<MissionDefinition> missions)
	{
		Missions = missions;
		ById = missions.ToDictionary(mission => mission.Id, StringComparer.Ordinal);
	}

	public IReadOnlyList<MissionDefinition> Missions { get; }

	public IReadOnlyDictionary<string, MissionDefinition> ById { get; }

	public IReadOnlyList<MissionDefinition> Of(MissionDifficulty difficulty)
		=> Missions.Where(mission => mission.Difficulty == difficulty).ToList();
}

/// <summary>Reads and validates missions.jsonc.</summary>
internal static class MissionFile
{
	public const string FileName = "missions.jsonc";
	private const string DefaultResource = "K4System.Missions.default-missions.jsonc";

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

	// SHA-256 of earlier default files (line endings and outer whitespace ignored). A missions.jsonc that still
	// matches one was never edited, so it is replaced with the new default list.
	private static readonly string[] OldDefaultHashes =
	[
		"40294c79ca1f80b33283bac4a8016508bbebba1e5ab6697d9323c0567ea0cb1f" // 6.1.0 to 6.2.3
	];

	/// <summary>True when the file is an unedited default list from an earlier version.</summary>
	public static bool IsOldDefault(string content)
	{
		string normalized = content.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
		string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
		return OldDefaultHashes.Contains(hash, StringComparer.Ordinal);
	}

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

	public static MissionPool Parse(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
			throw new InvalidDataException("The missions file is empty.");

		using JsonDocument document = JsonDocument.Parse(content, DocumentOptions);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("The missions file must contain a JSON object of missions.");

		List<MissionDefinition> missions = [];
		HashSet<string> ids = new(StringComparer.Ordinal);

		foreach (JsonProperty property in document.RootElement.EnumerateObject())
		{
			string id = property.Name.Trim();
			if (id.Length is 0 or > 64)
				throw new InvalidDataException($"Mission key '{property.Name}' must be 1 to 64 characters.");
			if (!ids.Add(id))
				throw new InvalidDataException($"Mission key '{id}' is used twice.");

			MissionDefinition mission;
			try
			{
				mission = property.Value.Deserialize<MissionDefinition>(EntryOptions)
					?? throw new InvalidDataException($"Mission '{id}' is empty.");
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException($"Mission '{id}' is invalid: {ex.Message}", ex);
			}

			mission.Id = id;
			Validate(mission);
			missions.Add(mission);
		}

		// Weekly missions are optional: without any, there is no weekly mission.
		foreach (MissionDifficulty difficulty in MissionRules.DailyDifficulties)
		{
			if (!missions.Any(mission => mission.Difficulty == difficulty))
				throw new InvalidDataException($"The missions file needs at least one {difficulty} mission.");
		}

		return new MissionPool(missions);
	}

	// Weapon names as CS2 reports them in kill events.
	private static readonly string[] KnownWeapons =
	[
		"ak47", "m4a1", "m4a1_silencer", "awp", "deagle", "usp_silencer", "glock", "hkp2000", "p250", "fiveseven", "tec9", "cz75a",
		"elite", "revolver", "mac10", "mp9", "mp7", "mp5sd", "ump45", "p90", "bizon", "nova", "xm1014", "sawedoff", "mag7",
		"m249", "negev", "famas", "galilar", "aug", "sg556", "ssg08", "scar20", "g3sg1", "knife", "bayonet", "taser",
		"hegrenade", "molotov", "inferno", "incgrenade", "flashbang", "smokegrenade", "decoy", "planted_c4"
	];

	/// <summary>Weapon missions whose weapon matches no CS2 weapon name, so they could never be finished.</summary>
	public static IEnumerable<string> WeaponWarnings(MissionPool pool)
	{
		foreach (MissionDefinition mission in pool.Missions)
		{
			if (mission.Type == MissionType.WeaponKills && mission.Weapon is string weapon
				&& !KnownWeapons.Any(known => known.Contains(weapon, StringComparison.Ordinal) || weapon.Contains(known, StringComparison.Ordinal)))
			{
				yield return $"Mission '{mission.Id}' uses the weapon '{weapon}', which matches no CS2 weapon name (for example ak47, m4a1, m4a1_silencer, awp, deagle, usp_silencer). It can never be finished.";
			}
		}
	}

	private static void Validate(MissionDefinition mission)
	{
		if (!Enum.IsDefined(mission.Type) || !Enum.IsDefined(mission.Difficulty))
			throw new InvalidDataException($"Mission '{mission.Id}' has an unknown Type or Difficulty.");
		if (mission.Target < 1)
			throw new InvalidDataException($"Mission '{mission.Id}' needs a Target of 1 or more.");
		if (mission.Reward is < 0)
			throw new InvalidDataException($"Mission '{mission.Id}' cannot have a negative Reward.");

		if (mission.Distance is not null && (mission.Type != MissionType.LongRangeKills || mission.Distance is < 1 or > 500))
			throw new InvalidDataException($"Mission '{mission.Id}': Distance is only for LongRangeKills, in meters from 1 to 500.");

		if (mission.Type == MissionType.WeaponKills)
		{
			if (string.IsNullOrWhiteSpace(mission.Weapon))
				throw new InvalidDataException($"Mission '{mission.Id}' is WeaponKills and needs a Weapon.");
			mission.Weapon = mission.Weapon.Trim().ToLowerInvariant().Replace("weapon_", "", StringComparison.Ordinal);
		}
	}
}
