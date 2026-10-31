namespace K4System.Api;

using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using K4SharedApi;
using K4System.Players;
using K4System.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Publishes <see cref="IPlayerAPI"/> to other plugins under "k4-system:sharedapi-player".
/// The interface, capability name, and exception behavior are a public contract.
/// </summary>
internal static class SharedPlayerApi
{
	public static PlayerCapability<IPlayerAPI> Capability { get; } = new("k4-system:sharedapi-player");

	private static readonly object Gate = new();
	private static Source? current;

	/// <summary>What the API reads from the running plugin.</summary>
	/// <param name="SetPoints">Game thread only.</param>
	internal sealed record Source(PlayerRegistry Players, Func<bool> IsActive, Func<bool> IsGameThread, Action<K4Player, int> SetPoints);

	private static readonly Func<CCSPlayerController, IPlayerAPI> Provider = controller =>
	{
		Source? source;
		lock (Gate)
			source = current;

		return source is null ? new UnavailablePlayerApi(controller) : new PlayerApi(controller, source);
	};

	public static void Register(Source source, ILogger logger)
	{
		lock (Gate)
			current = source;

		if (!TryReplaceProviders(out string? error))
		{
			logger.LogError("Unable to replace the K4 shared API provider: {ErrorMessage}", error);
			Capabilities.RegisterPlayerCapability(Capability, Provider);
		}
	}

	public static void Release(Source source)
	{
		lock (Gate)
		{
			if (ReferenceEquals(current, source))
				current = null;
		}
	}

	// CounterStrikeSharp has no way to unregister a player capability, and the first registered
	// provider always wins. After a hot reload the old provider would keep answering with the
	// unloaded plugin, so the provider list for this capability is replaced instead of appended.
	internal static bool TryReplaceProviders(out string? error)
	{
		error = null;

		try
		{
			FieldInfo? field = typeof(PlayerCapability<IPlayerAPI>).GetField("Providers", BindingFlags.NonPublic | BindingFlags.Static);

			if (field?.GetValue(null) is not Dictionary<string, List<Func<CCSPlayerController, IPlayerAPI>>> providers)
			{
				error = "CounterStrikeSharp player capability provider registry could not be resolved.";
				return false;
			}

			providers[Capability.Name] = [Provider];
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}
}

internal sealed class UnavailablePlayerApi(CCSPlayerController controller) : IPlayerAPI
{
	public bool IsLoaded => false;
	public bool IsValid => false;
	public bool IsPlayer => false;
	public CCSPlayerController Controller => controller;
	public int TopPlacement => throw new Exception("K4-SharedAPI > TopPlacement (get) > K4-System provider is not loaded.");
	public int Points
	{
		get => throw new Exception("K4-SharedAPI > Points (get) > K4-System provider is not loaded.");
		set => throw new Exception("K4-SharedAPI > Points (set) > K4-System provider is not loaded.");
	}
	public int RankID => throw new Exception("K4-SharedAPI > RankID (get) > K4-System provider is not loaded.");
	public string RankName => throw new Exception("K4-SharedAPI > RankName (get) > K4-System provider is not loaded.");
	public string RankClanTag => throw new Exception("K4-SharedAPI > RankClanTag (get) > K4-System provider is not loaded.");
}

internal sealed class PlayerApi(CCSPlayerController controller, SharedPlayerApi.Source source) : IPlayerAPI
{
	// The player list and CS2 entities are game-thread only. Other plugins also read from async continuations, so
	// off that thread the player comes from the registry's thread-safe copy and no entity is read.
	private K4Player? Player => source.IsGameThread() ? source.Players.Get(controller) : source.Players.FindFromAnyThread(controller);

	public bool IsLoaded => source.IsActive() && Player?.Rank is not null;
	public bool IsValid => source.IsGameThread() ? Player?.IsValid == true : Player is not null;
	public bool IsPlayer => Player?.IsHuman == true;
	public CCSPlayerController Controller => Player?.Controller ?? throw new Exception("K4-SharedAPI > Controller > Player is not valid or is not a player.");

	public int TopPlacement => Require("TopPlacement (get)").Rank!.TopPlacement;

	public int Points
	{
		get => Require("Points (get)").Rank!.Points;
		set
		{
			// Other plugins often set points from an async continuation. The player list and CS2 entities are game-thread
			// only, so off that thread the whole lookup runs on the next frame, and a player who left by then is skipped.
			if (source.IsGameThread())
			{
				source.SetPoints(Require("Points (set)"), value);
				return;
			}

			Server.NextFrame(() =>
			{
				if (source.IsActive() && Player is { IsValid: true, IsHuman: true, Rank: not null } player)
					source.SetPoints(player, value);
			});
		}
	}

	/// <summary>One-based: the default rank reports 0.</summary>
	public int RankID => Require("RankID (get)").Rank!.Rank.Id + 1;
	public string RankName => Require("RankName (get)").Rank!.Rank.Name;
	public string RankClanTag => Require("RankClanTag (get)").Rank!.Rank.Tag ?? "";

	private K4Player Require(string member)
	{
		K4Player? player = Player;
		if (player is null || (source.IsGameThread() && !player.IsValid) || !player.IsHuman || player.Rank is null)
			throw new Exception($"K4-SharedAPI > {member} > Player is not valid or is not a player.");
		return player;
	}
}
