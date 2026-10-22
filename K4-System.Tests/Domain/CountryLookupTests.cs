namespace K4System.Tests.Domain;

using K4System.Services;

public class CountryLookupTests
{
	[Theory]
	[InlineData("127.0.0.1:27015", "127.0.0.1")]
	[InlineData("[2001:db8::1]:27015", "2001:db8::1")]
	[InlineData("2001:db8::1", "2001:db8::1")]
	[InlineData(" 10.0.0.5 ", "10.0.0.5")]
	public void PlayerIpNormalizationHandlesIpv4AndIpv6(string input, string expected)
	{
		Assert.Equal(expected, CountryLookup.NormalizeIp(input));
	}

	[Theory]
	[InlineData("not-an-ip")]
	[InlineData("")]
	[InlineData(null)]
	public void InvalidIpIsRejected(string? input)
	{
		Assert.Null(CountryLookup.NormalizeIp(input));
	}
}
