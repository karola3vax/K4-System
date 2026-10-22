namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Ranks;

/// <summary>Shared helpers for the domain tests: eval loading, rank builders, and repository paths.</summary>
internal static class TestData
{
	public static List<T> LoadEvalList<T>(string fileName)
	{
		List<T>? cases = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(EvalPath(fileName)));
		Assert.NotNull(cases);
		Assert.NotEmpty(cases);
		return cases;
	}

	public static T LoadEval<T>(string fileName)
	{
		T? value = JsonSerializer.Deserialize<T>(File.ReadAllText(EvalPath(fileName)));
		Assert.NotNull(value);
		return value;
	}

	public static string EvalPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Evals", fileName);

	public static RankDefinition Rank(string name, int points, params string[] requiredPermissions) => new()
	{
		Key = name,
		Name = name,
		Point = points,
		Color = "default",
		RequiredPermissions = requiredPermissions.Length == 0 ? null : requiredPermissions.ToList()
	};

	/// <summary>The repository root, found by walking up from the test output directory.</summary>
	public static string RepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "global.json")) && Directory.Exists(Path.Combine(directory.FullName, "K4-System")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Unable to find the repository root from " + AppContext.BaseDirectory);
	}
}
