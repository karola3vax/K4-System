namespace K4System.Config;

using K4System.Missions;
using K4System.Ranks;

/// <summary>
/// Reads k4-system.jsonc, ranks.jsonc and missions.jsonc the way a fresh start would, without starting anything.
/// css_k4_reload only reloads when this finds no problem, so a typo can never leave the server without K4.
/// </summary>
internal static class ReloadCheck
{
	/// <param name="configDirectory">configs/plugins/K4-System.</param>
	/// <param name="moduleDirectory">plugins/K4-System, where ranks.jsonc and missions.jsonc live.</param>
	/// <returns>One line per problem; empty when a reload is safe.</returns>
	public static List<string> Run(string configDirectory, string moduleDirectory)
	{
		List<string> problems = [];

		string configPath = Path.Combine(configDirectory, ConfigFile.FileName);
		if (!File.Exists(configPath))
		{
			problems.Add($"{configPath} is missing.");
			return problems;
		}

		ConfigLoadResult result;
		try
		{
			result = ConfigFile.Parse(File.ReadAllText(configPath), configPath, created: false);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			problems.Add($"{configPath} could not be read: {ex.Message}");
			return problems;
		}

		if (result.Config is not PluginConfig config)
		{
			problems.Add(result.Error ?? $"{configPath} could not be read.");
			return problems;
		}

		try
		{
			ConfigValidator.ValidateAndNormalize(config);
		}
		catch (InvalidDataException ex)
		{
			problems.Add($"{configPath}: {ex.Message}");
			return problems;
		}

		// A broken ranks file falls back to the default ranks on start, which would silently change everyone's rank.
		if (config.Modules.Ranks)
			Check(Path.Join(moduleDirectory, RankFile.FileName), content => RankFile.Parse(content), problems);

		// A broken missions file turns missions off on start.
		if (config.Modules.Missions && config.Modules.Ranks)
			Check(Path.Join(moduleDirectory, MissionFile.FileName), content => MissionFile.Parse(content), problems);

		return problems;
	}

	// A missing file is fine: K4 writes the default one on start.
	private static void Check(string path, Action<string> parse, List<string> problems)
	{
		if (!File.Exists(path))
			return;

		try
		{
			parse(File.ReadAllText(path));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
		{
			problems.Add($"{path}: {ex.Message}");
		}
	}
}
