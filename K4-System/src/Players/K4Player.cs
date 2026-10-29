namespace K4System.Players;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using K4System.Missions;
using K4System.Ranks;

public sealed class RankState
{
	public RankState(int points, RankDefinition rank, int topPlacement)
	{
		Points = points;
		Rank = rank;
		TopPlacement = topPlacement;
	}

	public int Points { get; set; }

	public RankDefinition Rank { get; set; }

	/// <summary>Set at round start; round win and loss points need a full round on one team.</summary>
	public bool PlayedRound { get; set; }

	public int RoundPoints { get; set; }

	/// <summary>This round's points by reason key, in the order they first happened, for the round summary.</summary>
	public Dictionary<string, (int Count, int Points)> RoundReasons { get; } = new(StringComparer.Ordinal);

	public bool HideAdminTag { get; set; }

	public bool MuteMessages { get; set; }

	public int TopPlacement { get; set; }
}

/// <summary>
/// One connected player. Only touched on the game thread; background work receives snapshots instead.
/// </summary>
public sealed class K4Player
{
	public K4Player(CCSPlayerController controller)
	{
		Controller = controller;
		Slot = controller.Slot;
		SteamId = controller.SteamID;
		Name = controller.PlayerName;
		IsBot = controller.IsBot;
		IsHltv = controller.IsHLTV;
		LastActivityUtc = DateTime.UtcNow;
		LastPlaytimeRewardUtc = LastActivityUtc;
	}

	public CCSPlayerController Controller { get; }

	public int Slot { get; }

	public ulong SteamId { get; }

	public string Name { get; private set; }

	public bool IsBot { get; }

	public bool IsHltv { get; }

	/// <summary>A real person, not a bot or SourceTV.</summary>
	public bool IsHuman => !IsBot && !IsHltv;

	public bool HasController => Controller.IsValid;

	public bool HasPawn => Controller.PlayerPawn.IsValid;

	public bool IsValid => HasController && HasPawn && Controller.Connected == PlayerConnectedState.Connected;

	public RankState? Rank { get; set; }

	public StatCounters? Stats { get; set; }

	public PlaytimeCounters? Playtime { get; set; }

	public PlayerMissions? Missions { get; set; }

	public int KillStreak { get; set; }

	public DateTime LastKillUtc { get; set; } = DateTime.MinValue;

	public int MatchKills { get; set; }

	/// <summary>Kills on real players since the last death in this map, for the bounty.</summary>
	public int LifeKills { get; set; }

	public DateTime LastActivityUtc { get; set; }

	/// <summary>When the player first joined a team in the current match; null while they have not.</summary>
	public DateTime? MatchJoinedUtc { get; set; }

	public DateTime LastPlaytimeRewardUtc { get; set; }

	public DateTime? LastExpensiveCommandUtc { get; set; }

	public DateTime? LastPersonalResetCommandUtc { get; set; }

	public DateTime? LastPersonalRankResetUtc { get; set; }

	/// <summary>Permissions K4 granted for the current rank. Only these are ever removed again.</summary>
	public HashSet<string> OwnedPermissions { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Increases on every personal rank reset, so reward claims started before it are discarded.</summary>
	public int RewardResetVersion { get; set; }

	/// <summary>Reward commands waiting to run, by reward id.</summary>
	internal Dictionary<string, RewardCommand> PendingRewards { get; } = new(StringComparer.Ordinal);

	/// <summary>The clan tag K4 last applied, so K4 only clears its own tag.</summary>
	public string? AppliedClanTag { get; set; }

	/// <summary>Increases on every tag decision so late country lookups cannot overwrite a newer tag.</summary>
	public int ClanTagVersion { get; set; }

	public bool Matches(CCSPlayerController? controller)
		=> controller is not null && controller == Controller && (!controller.IsValid || controller.SteamID == SteamId);

	public void RefreshName()
	{
		if (HasController)
			Name = Controller.PlayerName;
	}

	public string ClanTag
	{
		get => Controller.Clan;
		set
		{
			Controller.Clan = value;
			Utilities.SetStateChanged(Controller, "CCSPlayerController", "m_szClan");
		}
	}
}
