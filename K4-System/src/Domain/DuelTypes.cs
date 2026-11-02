namespace K4System.Domain;

/// <summary>What counts in a duel: only kills between the two players that fit the type.</summary>
public enum DuelType
{
	Any,
	Noscope,
	Zeus,
	Knife,
	Scout,
	Headshot
}

/// <summary>A running duel as the panel and chat show it.</summary>
/// <param name="SecondsLeft">Until the time limit; the leader wins then, a tie returns both stakes.</param>
public sealed record DuelView(
	int Id,
	ulong Challenger,
	string ChallengerName,
	ulong Opponent,
	string OpponentName,
	int ChallengerKills,
	int OpponentKills,
	int Stake,
	DuelType Type,
	int Target,
	int SecondsLeft);

/// <summary>An open challenge waiting for the opponent's answer.</summary>
public sealed record DuelOffer(
	int Id,
	ulong Challenger,
	string ChallengerName,
	ulong Opponent,
	string OpponentName,
	int Stake,
	DuelType Type,
	int SecondsLeft);
