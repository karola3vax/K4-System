namespace K4System.Commands;

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using K4System.Services;

/// <summary>Shared checks for who may run a command and whether enough arguments were given.</summary>
internal sealed class CommandGuard(Chat chat)
{
	public static bool IsInGamePlayer(CCSPlayerController? player)
		=> player is not null && player.IsValid && player.PlayerPawn.Value is not null;

	public static string CallerName(CCSPlayerController? player)
		=> IsInGamePlayer(player) ? player!.PlayerName : "SERVER";

	public bool Check(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount = 0, string? help = null, string? permission = null)
	{
		switch (usage)
		{
			case CommandUsage.CLIENT_ONLY when !IsInGamePlayer(player):
				info.ReplyToCommand(chat.Prefixed("error.players-only"));
				return false;
			case CommandUsage.SERVER_ONLY when player is not null:
				info.ReplyToCommand(chat.Prefixed("error.server-only"));
				return false;
			case CommandUsage.CLIENT_AND_SERVER when player is not null && !IsInGamePlayer(player):
				return false;
		}

		if (!string.IsNullOrEmpty(permission) && player is not null && !AdminManager.PlayerHasPermissions(player, permission))
		{
			info.ReplyToCommand(chat.Prefixed("error.no-permission"));
			return false;
		}

		if (argCount > 0 && help is not null && info.ArgCount < argCount + 1)
		{
			info.ReplyToCommand(chat.Prefixed("usage", CommandName(info), help));
			return false;
		}

		return true;
	}

	/// <summary>The command as the player typed it in chat, without CounterStrikeSharp's css_ prefix.</summary>
	public static string CommandName(CommandInfo info)
	{
		string command = info.ArgByIndex(0);
		return command.StartsWith("css_", StringComparison.OrdinalIgnoreCase) ? command[4..] : command;
	}
}
