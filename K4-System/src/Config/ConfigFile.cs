namespace K4System.Config;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Result of reading k4-system.jsonc.</summary>
internal sealed record ConfigLoadResult(PluginConfig? Config, string Path, bool Created, string? Error);

/// <summary>
/// Reads configs/plugins/K4-System/k4-system.jsonc, writing a commented default file on first start.
/// 6.0 does not read the 5.x K4-System.json; if only that file exists, K4 stops and asks the owner
/// to move the settings over, so a MySQL server never starts on an empty database by accident.
/// </summary>
internal static class ConfigFile
{
	public const string FileName = "k4-system.jsonc";
	public const string LegacyFileName = "K4-System.json";
	private const string TemplateResource = "K4System.Config.default-config.jsonc";

	internal static readonly JsonSerializerOptions Options = new()
	{
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		PropertyNameCaseInsensitive = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
	};

	public static string Template
	{
		get
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResource)
				?? throw new InvalidOperationException($"Embedded resource {TemplateResource} is missing.");
			using StreamReader reader = new(stream);
			return reader.ReadToEnd();
		}
	}

	/// <summary>configs/plugins/K4-System, found from the plugin folder plugins/K4-System.</summary>
	public static string DirectoryFor(string moduleDirectory)
		=> Path.GetFullPath(Path.Combine(moduleDirectory, "..", "..", "configs", "plugins", "K4-System"));

	public static ConfigLoadResult Load(string directory)
	{
		string path = Path.Combine(directory, FileName);

		if (!File.Exists(path))
		{
			Directory.CreateDirectory(directory);
			File.WriteAllText(path, Template);

			if (File.Exists(Path.Combine(directory, LegacyFileName)))
			{
				return new ConfigLoadResult(null, path, true,
					$"K4-System 6.0 uses a new config file: {path}. Your old {LegacyFileName} is not read. " +
					"Copy your database and other settings into the new file, then restart. K4 will not track anything until then.");
			}

			return Parse(File.ReadAllText(path), path, created: true);
		}

		return Parse(File.ReadAllText(path), path, created: false);
	}

	internal static ConfigLoadResult Parse(string content, string path, bool created)
	{
		try
		{
			PluginConfig config = JsonSerializer.Deserialize<PluginConfig>(content, Options)
				?? throw new InvalidDataException("The file is empty.");

			if (config.Version != PluginConfig.CurrentVersion)
				throw new InvalidDataException($"version {config.Version} is not supported. This K4-System reads version {PluginConfig.CurrentVersion}.");

			return new ConfigLoadResult(config, path, created, null);
		}
		catch (Exception ex) when (ex is JsonException or InvalidDataException)
		{
			return new ConfigLoadResult(null, path, created, $"{path} could not be read: {ex.Message}");
		}
	}
}
