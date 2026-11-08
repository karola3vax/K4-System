namespace K4System.Tests.Domain;

using K4System.Services;

public class ClanTagTests
{
	// CS2 adds the outer brackets on the scoreboard, so K4 must not send its own: "[TOP #2 [Owner]]" was the bug.
	[Theory]
	[InlineData(new[] { "TOP #2", "[Owner]" }, "TOP #2 · Owner")]
	[InlineData(new[] { "[TOP #2]", "[Owner]" }, "TOP #2 · Owner")]
	[InlineData(new[] { "TR", "TOP #2 · Owner" }, "TR · TOP #2 · Owner")]
	[InlineData(new[] { "[SI]" }, "SI")]
	[InlineData(new[] { "Global Elite" }, "Global Elite")]
	[InlineData(new[] { " [ Admin ] " }, "Admin")]
	[InlineData(new[] { "[A] [B]" }, "[A] [B]")]
	[InlineData(new[] { "[]", "Owner" }, "Owner")]
	[InlineData(new[] { "", "" }, "")]
	public void Tags_drop_their_brackets_and_join_with_a_dot(string[] parts, string expected)
		=> Assert.Equal(expected, ClanTagService.Compose(parts));

	[Fact]
	public void Missing_parts_are_skipped()
		=> Assert.Equal("Owner", ClanTagService.Compose(null, "[Owner]"));
}
