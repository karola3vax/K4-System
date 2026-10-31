namespace K4System.Services;

using CounterStrikeSharp.API.Core;
using K4System.Players;
using K4System.Storage;

/// <summary>
/// Logs manual point changes (admin commands, resets, other plugins) so admins can see who changed a player's
/// points. Game events are not logged: they are too many and the round summary already shows them.
/// </summary>
internal sealed class PointAudit(SqlPlayerStore store, BackgroundWork background)
{
	public const string Set = "set";
	public const string Give = "give";
	public const string Remove = "remove";
	public const string Reset = "reset";
	public const string SelfReset = "self-reset";
	public const string Api = "api";

	public const string ApiActor = "shared API";

	/// <summary>Logs the change from <paramref name="before"/> to the player's points now. Nothing is logged when they are equal.</summary>
	public void Record(K4Player player, int before, string source, string actor)
	{
		if (player.Rank is not RankState state || state.Points == before)
			return;

		PointChange change = new(player.SteamId, state.Points - before, state.Points, source, actor);
		background.Run(() => store.LogPointChangesAsync([change]), "Logging a point change");
	}

	/// <summary>"Name (SteamID64)" for a player, "console" for the server.</summary>
	public static string ActorOf(CCSPlayerController? caller)
		=> caller is null ? "console" : $"{caller.PlayerName} ({caller.SteamID})";
}
