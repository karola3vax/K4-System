namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using K4System.Players;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

/// <summary>The CS2 game rules and player counts that decide whether points and stats count.</summary>
internal sealed class GameState(ILogger logger)
{
	public CCSGameRules? Rules { get; private set; }

	public bool HasRules => Rules is not null;

	public bool IsWarmup => Rules?.WarmupPeriod == true;

	public void Clear() => Rules = null;

	public void TryUpdate()
	{
		CCSGameRulesProxy? proxy;

		try
		{
			proxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
		}
		catch (Exception ex) when (IsEntitySystemUnavailable(ex))
		{
			logger.LogWarning("CS game rules are not ready yet: {ErrorMessage}", ex.Message);
			return;
		}
		catch (Exception ex)
		{
			logger.LogWarning("Unable to update CS game rules: {ErrorMessage}", ex.Message);
			return;
		}

		if (proxy?.GameRules is null)
		{
			logger.LogWarning("Unable to find CS game rules. Points and stats will wait until game rules are available.");
			return;
		}

		Rules = proxy.GameRules;
	}

	/// <summary>Connected human players with a real 17-digit SteamID64 and a pawn.</summary>
	public static bool TryCountEligibleHumans(out int count)
	{
		count = 0;

		try
		{
			count = Utilities.GetPlayers().Count(player =>
				player?.IsValid == true &&
				player.PlayerPawn?.IsValid == true &&
				!player.IsBot &&
				!player.IsHLTV &&
				player.SteamID.ToString().Length == 17 &&
				player.Connected == PlayerConnectedState.Connected);
			return true;
		}
		catch (Exception ex) when (IsEntitySystemUnavailable(ex))
		{
			return false;
		}
	}

	/// <summary>Points and stats both require game rules, a warmup check, and a minimum player count.</summary>
	public bool IsTrackingAllowed(bool countDuringWarmup, int minimumPlayers)
	{
		if (!HasRules || !TryCountEligibleHumans(out int humans))
			return false;

		return (!IsWarmup || countDuringWarmup) && minimumPlayers <= humans;
	}

	public static bool IsEntitySystemUnavailable(Exception ex)
		=> ex is NativeException && ex.Message.Contains("Entity system", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Localized chat text. Command replies already run in the caller's language; everything sent outside
/// a command uses the For/Tell methods so each player reads it in the language they chose with !lang.
/// Every K4 chat line starts with a space, which CS2 needs for colors.
/// </summary>
internal sealed class Chat(IStringLocalizer localizer)
{
	public string this[string key] => localizer[key];

	// Without arguments the text is returned as written: CounterStrikeSharp's argument overload always runs
	// string.Format, which throws on named tokens such as {name} in the join and leave messages.
	public string this[string key, params object[] args] => args.Length == 0 ? localizer[key] : localizer[key, args];

	public string Prefixed(string key, params object[] args)
		=> $" {localizer["prefix"]} {this[key, args]}";

	public string For(CCSPlayerController? player, string key, params object[] args)
		=> args.Length == 0 ? localizer.ForPlayer(player, key) : localizer.ForPlayer(player, key, args);

	public string PrefixedFor(CCSPlayerController? player, string key, params object[] args)
		=> $" {localizer.ForPlayer(player, "prefix")} {For(player, key, args)}";

	/// <summary>A message with the K4 prefix, in the player's language.</summary>
	public void Tell(CCSPlayerController player, string key, params object[] args)
		=> player.PrintToChat(PrefixedFor(player, key, args));

	/// <summary>A follow-up line without the prefix, in the player's language.</summary>
	public void TellLine(CCSPlayerController player, string key, params object[] args)
		=> player.PrintToChat(For(player, key, args));

	/// <summary>Sends a prefixed message to every human player, each in their own language.</summary>
	public void TellAll(string key, params object[] args)
	{
		foreach (CCSPlayerController player in Utilities.GetPlayers())
		{
			if (player.IsValid && !player.IsBot && !player.IsHLTV)
				Tell(player, key, args);
		}
	}
}

/// <summary>A language key and its arguments, translated for each reader.</summary>
internal readonly record struct ChatText(string Key, object[] Args);

/// <summary>Background tasks that should finish, or be logged, instead of being silently dropped.</summary>
internal sealed class BackgroundWork(ILogger logger)
{
	private readonly object gate = new();
	private readonly HashSet<Task> running = [];

	public void Run(Func<Task> work, string description)
	{
		Task task = Task.Run(work);

		lock (gate)
			running.Add(task);

		task.ContinueWith(completed =>
		{
			lock (gate)
				running.Remove(completed);

			if (completed.Exception is not null)
				logger.LogError("{Description} failed: {ErrorMessage}", description, completed.Exception.InnerException?.Message ?? completed.Exception.Message);
		}, TaskScheduler.Default);
	}

	public bool WaitAll(TimeSpan timeout)
	{
		Task[] tasks;
		lock (gate)
			tasks = running.ToArray();

		try
		{
			return tasks.Length == 0 || Task.WaitAll(tasks, timeout);
		}
		catch (AggregateException)
		{
			// Failures were already logged by the continuation.
			return true;
		}
	}
}

/// <summary>Runs work on the game thread, skipping it if the plugin has unloaded meanwhile.</summary>
internal sealed class GameThread(Func<bool> isActive)
{
	public bool IsActive => isActive();

	public void Post(Action action)
	{
		Server.NextFrame(() =>
		{
			if (isActive())
				action();
		});
	}

	public void PostWorldUpdate(Action action)
	{
		Server.NextWorldUpdate(() =>
		{
			if (isActive())
				action();
		});
	}
}

internal static class Permissions
{
	public const string Admin = "@k4system/admin";
	public const string VipMultiplier = "@k4system/vip/points-multiplier";

	/// <summary>"@flag" is a permission, "#group" a group, anything else a command override.</summary>
	public static bool Has(K4Player player, string permission)
	{
		if (string.IsNullOrWhiteSpace(permission))
			return false;

		permission = permission.Trim();

		return permission[0] switch
		{
			'@' => AdminManager.PlayerHasPermissions(player.Controller, permission),
			'#' => AdminManager.PlayerInGroup(player.Controller, permission),
			_ => AdminManager.GetPlayerCommandOverrideState(player.Controller, permission)
		};
	}
}
