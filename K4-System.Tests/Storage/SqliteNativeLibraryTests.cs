namespace K4System.Tests.Storage;

using System.Text.RegularExpressions;

/// <summary>
/// CS2's Linux servers run inside Valve's Steam Runtime 3 "sniper", which has glibc 2.31. A SQLite native library
/// built against a newer glibc cannot load there, and K4's SQLite storage then fails on every Linux server.
/// </summary>
public partial class SqliteNativeLibraryTests
{
	private const int SniperGlibcMinor = 31;

	[Fact]
	public void Linux_sqlite_library_loads_on_the_cs2_runtime()
	{
		string path = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "libe_sqlite3.so");
		Assert.True(File.Exists(path), $"Missing {path}");

		string symbols = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
		int newest = GlibcVersion().Matches(symbols).Select(match => int.Parse(match.Groups[1].Value)).Max();

		Assert.True(newest <= SniperGlibcMinor, $"libe_sqlite3.so needs glibc 2.{newest}; CS2's Linux runtime has 2.{SniperGlibcMinor}.");
	}

	[GeneratedRegex(@"GLIBC_2\.(\d+)")]
	private static partial Regex GlibcVersion();
}
