namespace K4System.Tests.Domain;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Translations;

public partial class CompatibilityTests
{
	[GeneratedRegex(@"\{(\d+)(?::[^}]*)?\}")]
	private static partial Regex NumericPlaceholder();

	[GeneratedRegex(@"\{([a-z]+)\}")]
	private static partial Regex NamedPlaceholder();

	[GeneratedRegex(@"^--\s+\d{4}\.\d{2}\.\d{2}\s+-\s+V(\S+)\s*$", RegexOptions.Multiline)]
	private static partial Regex ChangelogHeader();

	[Fact]
	public void CounterStrikeSharpCompatibilityEval()
	{
		CounterStrikeSharpCompatibilityEvalCase evalCase = TestData.LoadEval<CounterStrikeSharpCompatibilityEvalCase>("counterstrikesharp-compatibility.json");
		MinimumApiVersion? minimumApiVersion = typeof(Plugin).GetCustomAttribute<MinimumApiVersion>();

		Assert.Equal(evalCase.ExpectedPackageVersion, typeof(BasePlugin).Assembly.GetName().Version?.ToString());
		Assert.Equal(evalCase.ExpectedMinimumApiVersion, minimumApiVersion?.Version);
	}

	[Fact]
	public void ReleaseVersionMatchesTheLatestChangelogEntry()
	{
		// ModuleVersion does not read plugin state, so an uninitialized instance avoids the BasePlugin constructor.
		Plugin plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
		string changelog = File.ReadAllText(Path.Combine(TestData.RepositoryRoot(), "CHANGELOG"));
		MatchCollection headers = ChangelogHeader().Matches(changelog);

		Assert.NotEmpty(headers);
		Assert.StartsWith(headers[0].Groups[1].Value + " ", plugin.ModuleVersion, StringComparison.Ordinal);
	}

	[Fact]
	public void BundledLanguagesHaveMatchingKeysAndValidFormats()
	{
		string languageDirectory = Path.Combine(AppContext.BaseDirectory, "lang");
		string[] languageFiles = Directory.GetFiles(languageDirectory, "*.json");

		string[] expected =
		[
			"ar", "az", "bg", "cs", "da", "de", "el", "en", "es", "et", "fi", "fr", "hu", "id", "it", "ja", "kk", "ko", "lt",
			"lv", "nl", "no", "pl", "pt-BR", "pt-PT", "ro", "ru", "sk", "sr", "sv", "th", "tr", "uk", "vi", "zh-CN", "zh-TW"
		];
		Assert.Equal(expected.Order(), languageFiles.Select(Path.GetFileNameWithoutExtension).Order()!);

		// Every file name must be a culture CounterStrikeSharp can select with !lang.
		foreach (string code in expected)
			Assert.Equal(code, System.Globalization.CultureInfo.GetCultureInfo(code).Name);

		Dictionary<string, string> english = ReadLanguage(Path.Combine(languageDirectory, "en.json"));

		foreach (string languageFile in languageFiles)
		{
			string fileName = Path.GetFileName(languageFile);
			Dictionary<string, string> language = ReadLanguage(languageFile);

			Assert.True(english.Keys.Order().SequenceEqual(language.Keys.Order()),
				$"{fileName} keys differ from en.json. Missing: [{string.Join(", ", english.Keys.Except(language.Keys))}] Extra: [{string.Join(", ", language.Keys.Except(english.Keys))}]");

			foreach ((string key, string englishValue) in english)
			{
				string value = language[key];
				Assert.False(string.IsNullOrWhiteSpace(value), $"{fileName} {key} is blank.");
				Assert.True(NumericPlaceholders(englishValue).SequenceEqual(NumericPlaceholders(value)), $"{fileName} {key} placeholders differ from en.json.");
				Assert.True(NamedTokens(englishValue).SequenceEqual(NamedTokens(value)), $"{fileName} {key} color tags or named placeholders differ from en.json.");
				Assert.True(englishValue.StartsWith(' ') == value.StartsWith(' '), $"{fileName} {key} must keep the leading space of en.json.");

				// Values with named placeholders such as {name} are filled by plugin code, not string.Format.
				if (!NumericPlaceholders(value).Any())
					continue;

				try
				{
					_ = string.Format(CultureInfo.InvariantCulture, value.ReplaceColorTags(), Enumerable.Repeat<object>(1, 10).ToArray());
				}
				catch (FormatException ex)
				{
					Assert.Fail($"{fileName} {key} is not a valid format string: {ex.Message}");
				}
			}
		}
	}

	private static Dictionary<string, string> ReadLanguage(string path)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
		return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty);
	}

	private static IEnumerable<string> NamedTokens(string value)
		=> NamedPlaceholder().Matches(value).Select(match => match.Groups[1].Value).Distinct().Order();

	private static IEnumerable<int> NumericPlaceholders(string value)
		=> NumericPlaceholder().Matches(value)
			.Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
			.Distinct()
			.Order();

	public sealed class CounterStrikeSharpCompatibilityEvalCase
	{
		public string ExpectedPackageVersion { get; set; } = string.Empty;
		public int ExpectedMinimumApiVersion { get; set; }
	}
}
