namespace K4System.Tests.Domain;

using System.Globalization;
using CounterStrikeSharp.API.Core.Translations;
using K4System.Services;

/// <summary>
/// A server's own wording lives outside the plugin folder, so pasting an update over plugins/K4-System
/// keeps it (6.2.2: updates replaced lang/*.json and wiped every edited line).
/// </summary>
public sealed class CustomLanguageTests : IDisposable
{
	private readonly string root = Path.Combine(Path.GetTempPath(), "k4-lang-" + Guid.NewGuid().ToString("N"));
	private readonly string shippedDirectory;
	private readonly string customDirectory;
	private readonly CultureInfo? defaultCulture = CultureInfo.DefaultThreadCurrentUICulture;

	public CustomLanguageTests()
	{
		shippedDirectory = Path.Combine(root, "plugins", "lang");
		customDirectory = Path.Combine(root, "configs", "lang");
		Directory.CreateDirectory(shippedDirectory);
		File.WriteAllText(Path.Combine(shippedDirectory, "en.json"), """{ "prefix": "{lime}[K4]", "hello": "Hello {0}", "only-english": "English only" }""");
		File.WriteAllText(Path.Combine(shippedDirectory, "tr.json"), """{ "prefix": "{lime}[K4]", "hello": "Merhaba {0}" }""");
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
	}

	public void Dispose()
	{
		CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
		Directory.Delete(root, recursive: true);
	}

	private CustomLanguage Language(params (string File, string Text)[] files)
	{
		Directory.CreateDirectory(customDirectory);
		foreach ((string file, string text) in files)
			File.WriteAllText(Path.Combine(customDirectory, file), text);
		var (lines, _) = CustomLanguage.Load(customDirectory);
		return new CustomLanguage(new JsonStringLocalizer(shippedDirectory), shippedDirectory, lines);
	}

	private static string In(string culture, Func<string> read)
	{
		CultureInfo before = CultureInfo.CurrentUICulture;
		CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
		try
		{
			return read();
		}
		finally
		{
			CultureInfo.CurrentUICulture = before;
		}
	}

	[Fact]
	public void TheServersLinesWinAndEveryOtherLineKeepsTheShippedText()
	{
		CustomLanguage language = Language(("en.json", """{ "prefix": "{gold}[MyServer]" }"""));
		Assert.Equal(" \u0010[MyServer]", In("en", () => language["prefix"]));
		Assert.Equal("Hello Ana", In("en", () => language["hello", "Ana"]));
	}

	[Fact]
	public void PlaceholdersWorkInTheServersLines()
	{
		CustomLanguage language = Language(("en.json", """{ "hello": "Welcome back, {0}!" }"""));
		Assert.Equal("Welcome back, Ana!", In("en", () => language["hello", "Ana"]));
	}

	[Fact]
	public void AShippedTranslationIsNotReplacedByTheServersEnglishLine()
	{
		CustomLanguage language = Language(("en.json", """{ "hello": "Hi {0}" }"""));
		Assert.Equal("Merhaba Ana", In("tr", () => language["hello", "Ana"]));
	}

	[Fact]
	public void ALineWithNoTranslationUsesTheServersLineInTheServerLanguage()
	{
		CustomLanguage language = Language(("en.json", """{ "only-english": "Custom English" }"""));
		Assert.Equal("Custom English", In("tr", () => language["only-english"]));
	}

	[Fact]
	public void ARegionalLanguageUsesItsParentsLines()
	{
		CustomLanguage language = Language(("pt.json", """{ "prefix": "[Servidor]" }"""));
		Assert.Equal("[Servidor]", In("pt-BR", () => language["prefix"]));
	}

	[Fact]
	public void ABrokenFileIsReportedAndItsLanguageKeepsTheShippedText()
	{
		Directory.CreateDirectory(customDirectory);
		File.WriteAllText(Path.Combine(customDirectory, "en.json"), """{ "prefix": "[MyServer]" """);
		File.WriteAllText(Path.Combine(customDirectory, "tr.json"), """{ "prefix": 5 }""");
		var (lines, files) = CustomLanguage.Load(customDirectory);
		Assert.Empty(lines);
		Assert.All(files, file => Assert.NotNull(file.Problem));
		CustomLanguage language = new(new JsonStringLocalizer(shippedDirectory), shippedDirectory, lines);
		Assert.Equal(" \u0006[K4]", In("en", () => language["prefix"]));
	}

	[Fact]
	public void TheFolderGetsAHowToFileAndAnUpdateDoesNotTouchIt()
	{
		Language(("en.json", """{ "prefix": "[MyServer]" }"""));
		Assert.True(File.Exists(Path.Combine(customDirectory, CustomLanguage.ReadmeName)));
		Assert.False(customDirectory.StartsWith(Path.GetDirectoryName(shippedDirectory)!, StringComparison.Ordinal));
	}
}
