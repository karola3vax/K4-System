namespace K4System.Tests.Domain;

using K4System.Domain;

public class ShowdownsTests
{
	[Theory]
	[InlineData(5000, 2000, 10, 200)]
	[InlineData(2000, 5000, 10, 200)]
	[InlineData(99, 5000, 10, 9)]
	[InlineData(0, 5000, 10, 0)]
	[InlineData(-50, 5000, 10, 0)]
	[InlineData(int.MaxValue, int.MaxValue, 100, int.MaxValue)]
	[InlineData(1000, 1000, 250, 1000)]
	public void MaxStakeIsAShareOfThePoorerPlayersPoints(int challenger, int opponent, int percent, int expected)
		=> Assert.Equal(expected, Showdowns.MaxStake(challenger, opponent, percent));

	[Theory]
	[InlineData(true, 5, true, 3, 1)]
	[InlineData(true, 3, true, 5, -1)]
	[InlineData(true, 4, true, 4, 0)]
	[InlineData(true, 0, false, 9, 1)]
	[InlineData(false, 9, true, 0, -1)]
	[InlineData(false, 9, false, 1, 0)]
	public void LeaverLosesAndMoreKillsWins(bool challengerHere, int challengerKills, bool opponentHere, int opponentKills, int expected)
		=> Assert.Equal(expected, Showdowns.Winner(challengerHere, challengerKills, opponentHere, opponentKills));

	// Regression: on unload the leaver's stake used to vanish instead of going to the player still here.
	[Theory]
	[InlineData(true, true, 100, 100, 100)]
	[InlineData(true, false, 100, 200, 0)]
	[InlineData(false, true, 100, 0, 200)]
	[InlineData(false, false, 100, 0, 0)]
	public void UnloadReturnsStakesAndALeaverLoses(bool challengerHere, bool opponentHere, int stake, int toChallenger, int toOpponent)
		=> Assert.Equal((toChallenger, toOpponent), Showdowns.UnloadRefund(challengerHere, opponentHere, stake));

	[Theory]
	[InlineData("1.2.3.4:27005", "1.2.3.4:27006", true)]
	[InlineData("1.2.3.4:27005", "1.2.3.5:27005", false)]
	[InlineData("[2001:db8::1]:27005", "[2001:db8::1]:27015", true)]
	[InlineData("", "", false)]
	[InlineData(null, null, false)]
	[InlineData("loopback", "loopback", false)]
	[InlineData("127.0.0.1:27005", "127.0.0.1:27006", false)]
	public void SameAddressIgnoresPortsAndUnknownAddresses(string? left, string? right, bool expected)
		=> Assert.Equal(expected, Showdowns.SameAddress(left, right));
}
