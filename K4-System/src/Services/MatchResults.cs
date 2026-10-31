namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Players;

/// <summary>Who played and who won a finished match. In free-for-all the player with the most match kills wins.</summary>
internal static class MatchResults
{
	/// <param name="HasResult">False only for a team draw. A winning team with no humans left still counts as a result.</param>
	public static (List<K4Player> Played, HashSet<K4Player> Winners, bool HasResult) Get(PlayerRegistry players, bool freeForAll)
	{
		List<K4Player> played = players.ValidHumans.Where(player => player.Controller.Team > CsTeam.Spectator).ToList();
		HashSet<K4Player> winners = [];

		if (freeForAll)
		{
			// Nobody wins a match without a kill, so an empty or bot-only match is no result.
			K4Player? winner = played.Where(player => player.MatchKills > 0).OrderByDescending(player => player.MatchKills).ThenBy(player => player.SteamId).FirstOrDefault();
			if (winner is not null)
				winners.Add(winner);
			return (played, winners, winner is not null);
		}

		List<CCSTeam> teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager").ToList();
		int ctScore = teams.Where(team => team.Teamname == "CT").Select(team => team.Score).FirstOrDefault();
		int tScore = teams.Where(team => team.Teamname == "TERRORIST").Select(team => team.Score).FirstOrDefault();
		CsTeam winnerTeam = ctScore > tScore ? CsTeam.CounterTerrorist : tScore > ctScore ? CsTeam.Terrorist : CsTeam.None;

		if (winnerTeam > CsTeam.Spectator)
			winners.UnionWith(played.Where(player => player.Controller.Team == winnerTeam));

		return (played, winners, winnerTeam > CsTeam.Spectator);
	}
}
