namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Missions;
using K4System.Ranks;

/// <summary>css_k4_reload reloads only when a fresh start would read every K4 file without a problem.</summary>
public sealed class ReloadCheckTests : IDisposable
{
	private readonly string configDirectory = Path.Combine(Path.GetTempPath(), "k4-reload-" + Guid.NewGuid().ToString("N"), "configs");
	private readonly string moduleDirectory;

	public ReloadCheckTests()
	{
		moduleDirectory = Path.Combine(Path.GetDirectoryName(configDirectory)!, "plugin");
		Directory.CreateDirectory(configDirectory);
		Directory.CreateDirectory(moduleDirectory);
		File.WriteAllText(Path.Combine(configDirectory, ConfigFile.FileName), ConfigFile.Template);
		File.WriteAllText(Path.Combine(moduleDirectory, RankFile.FileName), RankFile.DefaultContent);
		File.WriteAllText(Path.Combine(moduleDirectory, MissionFile.FileName), MissionFile.DefaultContent);
	}

	public void Dispose() => Directory.Delete(Path.GetDirectoryName(configDirectory)!, recursive: true);

	private List<string> Check() => ReloadCheck.Run(configDirectory, moduleDirectory);

	[Fact]
	public void The_shipped_files_pass()
		=> Assert.Empty(Check());

	[Fact]
	public void Missing_rank_and_mission_files_pass_because_a_start_writes_them()
	{
		File.Delete(Path.Combine(moduleDirectory, RankFile.FileName));
		File.Delete(Path.Combine(moduleDirectory, MissionFile.FileName));
		Assert.Empty(Check());
	}

	[Theory]
	[InlineData("\"storage\": {", "\"storage\": { \"typo\": 1,", "typo")]
	[InlineData("\"type\": \"sqlite\"", "\"type\": \"postgres\"", "postgres")]
	[InlineData("\"preset\": \"casual\"", "\"preset\": \"ultra\"", "ultra")]
	[InlineData("\"version\": 1", "\"version\": 1 ,,", "could not be read")]
	public void A_broken_config_is_named_and_stops_the_reload(string find, string replace, string mentioned)
	{
		string path = Path.Combine(configDirectory, ConfigFile.FileName);
		File.WriteAllText(path, File.ReadAllText(path).Replace(find, replace, StringComparison.Ordinal));

		List<string> problems = Check();

		Assert.Single(problems);
		Assert.Contains(mentioned, problems[0], StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void A_missing_config_stops_the_reload()
	{
		File.Delete(Path.Combine(configDirectory, ConfigFile.FileName));
		Assert.Contains("missing", Assert.Single(Check()));
	}

	[Fact]
	public void Broken_rank_and_mission_files_are_both_named()
	{
		File.WriteAllText(Path.Combine(moduleDirectory, RankFile.FileName), "{ \"Silver\": 5 }");
		File.WriteAllText(Path.Combine(moduleDirectory, MissionFile.FileName), "not json");

		List<string> problems = Check();

		Assert.Equal(2, problems.Count);
		Assert.Contains(problems, problem => problem.Contains(RankFile.FileName, StringComparison.Ordinal));
		Assert.Contains(problems, problem => problem.Contains(MissionFile.FileName, StringComparison.Ordinal));
	}

	[Fact]
	public void A_broken_missions_file_is_ignored_while_missions_are_off()
	{
		string path = Path.Combine(configDirectory, ConfigFile.FileName);
		File.WriteAllText(path, File.ReadAllText(path).Replace("\"missions\": true", "\"missions\": false", StringComparison.Ordinal));
		File.WriteAllText(Path.Combine(moduleDirectory, MissionFile.FileName), "not json");

		Assert.Empty(Check());
	}
}
