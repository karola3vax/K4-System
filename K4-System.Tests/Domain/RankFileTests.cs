namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Ranks;

public class RankFileTests
{
	private const string NoneRank = "\"None\":{\"Name\":\"None\",\"Point\":-1,\"Color\":\"default\"}";

	[Fact]
	public void ValidRanksAreParsed()
	{
		List<RankDefinition> ranks = RankFile.Parse("{" + NoneRank + ",\"Gold\":{\"Name\":\"Gold\",\"Point\":100,\"Color\":\"gold\"}}");

		Assert.Equal(2, ranks.Count);
		Assert.Equal(["None", "Gold"], ranks.Select(rank => rank.Key));
	}

	[Theory]
	[InlineData("{\"\":{\"Name\":\"None\",\"Point\":-1,\"Color\":\"default\"}}")]
	[InlineData("{\"Gold\":{\"Name\":\"Gold\",\"Point\":100,\"Color\":\"gold\"}}")]
	[InlineData("{" + NoneRank + ",\"Other\":{\"Name\":\"None\",\"Point\":100,\"Color\":\"gold\"}}")]
	[InlineData("{" + NoneRank + ",\"Bad\":{\"Name\":\"Bad\",\"Point\":-2,\"Color\":\"red\"}}")]
	[InlineData("{" + NoneRank + ",\"Bad\":{\"Name\":\"Bad\",\"Point\":1,\"Color\":\"red\",\"Permissions\":[{\"DisplayName\":\"Bad\",\"PermissionName\":\"css_bad\"}]}}")]
	[InlineData("{" + NoneRank + ",\"Bad\":{\"Name\":\"Bad\",\"Point\":1,\"Color\":\"red\",\"Permissions\":[{\"DisplayName\":\"Bad\",\"PermissionName\":\"@css/root\"}]}}")]
	[InlineData("{" + NoneRank + ",\"Bad\":{\"Name\":\"Bad\",\"Point\":1,\"Color\":\"red\",\"Rewards\":[{\"Type\":\"item\",\"Value\":\"crate\"}]}}")]
	public void RankConfigurationRejectsUnsafeInput(string content)
	{
		Assert.Throws<InvalidDataException>(() => RankFile.Parse(content));
	}

	[Fact]
	public void GeneratedDefaultRanksMatch100kLadder()
	{
		List<RankDefinition> ranks = RankFile.Parse(RankFile.DefaultContent);
		(string Name, int Points)[] expected =
		[
			("None", -1),
			("Silver I", 500),
			("Silver II", 1500),
			("Silver III", 3000),
			("Silver IV", 5000),
			("Silver Elite", 7500),
			("Silver Elite Master", 10000),
			("Gold Nova I", 15000),
			("Gold Nova II", 20000),
			("Gold Nova III", 25000),
			("Gold Nova Master", 30000),
			("Master Guardian I", 35000),
			("Master Guardian II", 40000),
			("Master Guardian Elite", 50000),
			("Distinguished Master Guardian", 60000),
			("Legendary Eagle", 70000),
			("Legendary Eagle Master", 80000),
			("Supreme Master First Class", 90000),
			("Global Elite", 100000)
		];

		Assert.Equal(expected.Length, ranks.Count);

		foreach ((string name, int points) in expected)
		{
			RankDefinition? rank = ranks.SingleOrDefault(candidate => candidate.Key == name);
			Assert.True(rank is not null, $"Missing generated rank '{name}'.");
			Assert.Equal(points, rank.Point);
			if (points > 0)
				Assert.Equal(0, points % 500);
		}

		Assert.Equal(10000, ranks.Single(rank => rank.Key == "Global Elite").Point - ranks.Single(rank => rank.Key == "Supreme Master First Class").Point);
	}

	[Fact]
	public void GeneratedRankExamplesAreCommentsOnly()
	{
		string content = RankFile.DefaultContent;
		RankDefinition silverOne = RankFile.Parse(content).Single(rank => rank.Key == "Silver I");

		Assert.Contains("// \"RequiredPermissions\"", content);
		Assert.Contains("// \"RewardDescription\"", content);
		Assert.Contains("// \"Rewards\"", content);
		Assert.Contains("// \"Permissions\"", content);
		Assert.Null(silverOne.Permissions);
		Assert.Null(silverOne.RequiredPermissions);
		Assert.Null(silverOne.Rewards);
	}

	[Fact]
	public void ParseAcceptsCommentsAndTrailingCommas()
	{
		const string content = """
			{
				// line comment
				"None": { "Name": "None", "Point": -1, "Color": "default", }, /* block comment */
				"Gold": {
					"Name": "Gold", // trailing comment
					"Point": 100,
					"Color": "gold",
				},
			}
			""";

		List<RankDefinition> ranks = RankFile.Parse(content);

		Assert.Equal(["None", "Gold"], ranks.Select(rank => rank.Key));
		Assert.Equal(100, ranks[1].Point);
	}

	[Fact]
	public void ParseUsesCaseInsensitivePropertyNames()
	{
		const string content = """
			{
				"None": { "name": "None", "POINT": -1, "color": "default" },
				"Gold": { "NAME": "Gold", "point": 100, "CoLoR": "gold", "requiredpermissions": ["@css/vip"] }
			}
			""";

		List<RankDefinition> ranks = RankFile.Parse(content);
		RankDefinition gold = ranks[1];

		Assert.Equal("Gold", gold.Name);
		Assert.Equal(100, gold.Point);
		Assert.Equal("gold", gold.Color);
		Assert.Equal(["@css/vip"], gold.RequiredPermissions!);
	}

	[Fact]
	public void ParseAcceptsNumbersWrittenAsStrings()
	{
		const string content = """
			{
				"None": { "Name": "None", "Point": "-1", "Color": "default" },
				"Gold": { "Name": "Gold", "Point": "250", "Color": "gold" }
			}
			""";

		List<RankDefinition> ranks = RankFile.Parse(content);

		Assert.Equal(-1, ranks[0].Point);
		Assert.Equal(250, ranks[1].Point);
	}

	[Fact]
	public void ParseKeepsFileOrderForEqualPointRanks()
	{
		const string content = """
			{
				"None": { "Name": "None", "Point": -1, "Color": "default" },
				"Second": { "Name": "Second", "Point": 100, "Color": "red" },
				"First": { "Name": "First", "Point": 100, "Color": "blue" },
				"Third": { "Name": "Third", "Point": 100, "Color": "green" }
			}
			""";

		List<RankDefinition> ranks = RankFile.Parse(content);
		Assert.Equal(["None", "Second", "First", "Third"], ranks.Select(rank => rank.Key));

		RankLadder ladder = RankLadder.Create(ranks, RankGameMode.Normal);
		Assert.Equal(["None", "Second", "First", "Third"], ladder.Ranks.Select(rank => rank.Key));
		Assert.Equal("Third", ladder.Resolve(100, _ => false).Key);
	}

	[Fact]
	public void DuplicatedKeyReplacesEarlierEntryInPlace()
	{
		const string content = """
			{
				"None": { "Name": "None", "Point": -1, "Color": "default" },
				"Gold": { "Name": "Old Gold", "Point": 100, "Color": "gold" },
				"Silver": { "Name": "Silver", "Point": 50, "Color": "silver" },
				"Gold": { "Name": "New Gold", "Point": 300, "Color": "yellow" }
			}
			""";

		List<RankDefinition> ranks = RankFile.Parse(content);

		Assert.Equal(["None", "Gold", "Silver"], ranks.Select(rank => rank.Key));
		Assert.Equal("New Gold", ranks[1].Name);
		Assert.Equal(300, ranks[1].Point);
		Assert.DoesNotContain(ranks, rank => rank.Name == "Old Gold");
	}
}
