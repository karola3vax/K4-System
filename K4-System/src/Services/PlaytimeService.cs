namespace K4System.Services;

using CounterStrikeSharp.API.Modules.Utils;
using K4System.Players;

/// <summary>
/// Playtime counters. Running time is added to the stored counters whenever the player's
/// team or life state changes, and before every save that ends a session.
/// </summary>
internal sealed class PlaytimeService(Chat chat)
{
	public void OnTeamChange(K4Player player, int oldTeam)
	{
		PlaytimeCounters? playtime = player.Playtime;
		if (playtime is null)
			return;

		DateTime now = DateTime.UtcNow;
		playtime.AddElapsed(PlaytimeField.All, PlaytimeAnchor.Connect, now);

		if (oldTeam != (int)CsTeam.None)
		{
			playtime.AddElapsed(PlaytimeCounters.FieldForTeam(oldTeam), PlaytimeAnchor.Team, now);

			if (oldTeam > (int)CsTeam.Spectator && player.HasPawn)
				playtime.AddElapsed(player.Controller.PawnIsAlive ? PlaytimeField.Alive : PlaytimeField.Dead, PlaytimeAnchor.Death, now);
		}

		playtime.SetAnchor(PlaytimeAnchor.Team, now);
		playtime.SetAnchor(PlaytimeAnchor.Death, now);
	}

	public void OnSpawn(K4Player player)
	{
		DateTime now = DateTime.UtcNow;
		player.Playtime?.AddElapsed(PlaytimeField.All, PlaytimeAnchor.Connect, now);
		player.Playtime?.AddElapsed(PlaytimeField.Dead, PlaytimeAnchor.Death, now);
	}

	public void OnDeath(K4Player player)
	{
		DateTime now = DateTime.UtcNow;
		player.Playtime?.AddElapsed(PlaytimeField.All, PlaytimeAnchor.Connect, now);
		player.Playtime?.AddElapsed(PlaytimeField.Alive, PlaytimeAnchor.Death, now);
	}

	/// <summary>Adds all running time, then restarts the counters. Used on disconnect, map end, and unload.</summary>
	public void Settle(K4Player player)
	{
		PlaytimeCounters? playtime = player.Playtime;
		if (playtime is null || !player.HasController)
			return;

		DateTime now = DateTime.UtcNow;
		int team = player.Controller.TeamNum;

		playtime.AddElapsed(PlaytimeField.All, PlaytimeAnchor.Connect, now);
		playtime.AddElapsed(PlaytimeCounters.FieldForTeam(team), PlaytimeAnchor.Team, now);

		if (team > (int)CsTeam.Spectator && player.HasPawn)
			playtime.AddElapsed(player.Controller.PawnIsAlive ? PlaytimeField.Alive : PlaytimeField.Dead, PlaytimeAnchor.Death, now);

		playtime.RestartAnchors(now);
	}

	/// <summary>Stored playtime plus the time still running, without changing the counters.</summary>
	public int[] CurrentSeconds(K4Player player)
	{
		PlaytimeCounters playtime = player.Playtime!;
		int[] seconds = playtime.ToArray();
		DateTime now = DateTime.UtcNow;
		int team = player.Controller.TeamNum;

		int Since(PlaytimeAnchor anchor) => (int)Math.Max(0, (now - playtime.AnchorOf(anchor)).TotalSeconds);

		seconds[(int)PlaytimeField.All] += Since(PlaytimeAnchor.Connect);
		seconds[(int)PlaytimeCounters.FieldForTeam(team)] += Since(PlaytimeAnchor.Team);

		if (team > (int)CsTeam.Spectator)
			seconds[(int)(player.Controller.PawnIsAlive ? PlaytimeField.Alive : PlaytimeField.Dead)] += Since(PlaytimeAnchor.Death);

		return seconds;
	}

	public string Format(int seconds)
		=> PlaytimeCounters.Format(seconds, unit => chat[$"unit.{unit}"]);
}
