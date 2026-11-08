namespace K4System.Services;

using K4System.Config;
using K4System.Players;

/// <summary>
/// Builds the scoreboard clan tag from the country, top placement, admin, and rank tags.
/// K4 only clears a tag it applied itself, so other plugins' tags are left alone.
/// </summary>
internal sealed class ClanTagService(PluginConfig config, CountryLookup countries, GameThread gameThread)
{
	public void Apply(K4Player player)
	{
		TagSettings settings = config.Tags;
		RankState? rank = player.Rank;
		string? tag = null;
		string? top = null;
		bool hasTagSource = false;

		if (settings.RankTags && rank is not null)
		{
			tag = rank.Rank.Tag ?? rank.Rank.Name;
			hasTagSource = true;
		}

		if (rank?.HideAdminTag == false)
		{
			foreach (AdminTag admin in settings.Admins)
			{
				if (admin.Tag is null || !Permissions.Has(player, admin.Permission))
					continue;

				tag = admin.Tag;
				hasTagSource = true;
				break;
			}
		}

		if (settings.TopPlacement.Enabled && rank is not null && rank.TopPlacement != 0 && rank.TopPlacement <= settings.TopPlacement.Max)
		{
			top = settings.TopPlacement.Format.Replace("{placement}", rank.TopPlacement.ToString());
			hasTagSource = true;
		}

		string combined = Compose(top, tag);
		int version = ++player.ClanTagVersion;

		if (!settings.Country)
		{
			if (hasTagSource)
				SetTag(player, combined, version);
			else
				Release(player);
			return;
		}

		string? ip = player.Controller.IpAddress;
		countries.GetCountryCodeAsync(ip).ContinueWith(lookup =>
		{
			string country = lookup.IsCompletedSuccessfully ? lookup.Result : CountryLookup.Unknown;
			string complete = Compose(country, combined);
			gameThread.PostWorldUpdate(() => SetTag(player, complete, version));
		}, TaskScheduler.Default);
	}

	/// <summary>
	/// CS2 draws its own brackets around a clan tag, so "[Owner]" from a config would show as "[[Owner]]".
	/// Each part loses one pair of outer brackets, and the parts are joined with a dot: [TOP #2 · Owner].
	/// </summary>
	internal static string Compose(params string?[] parts)
		=> string.Join(" · ", parts.Select(Unwrap).Where(part => part.Length > 0));

	private static string Unwrap(string? part)
	{
		string text = part?.Trim() ?? string.Empty;
		// Only a single wrapping pair: "[A] [B]" stays as it is.
		bool wrapped = text.Length >= 2 && text[0] == '[' && text.IndexOf(']') == text.Length - 1 && text.LastIndexOf('[') == 0;
		return wrapped ? text[1..^1].Trim() : text;
	}

	public void Release(K4Player player)
	{
		++player.ClanTagVersion;

		if (!player.HasController || player.AppliedClanTag is null)
			return;

		if (player.ClanTag == player.AppliedClanTag)
			player.ClanTag = string.Empty;

		player.AppliedClanTag = null;
	}

	private static void SetTag(K4Player player, string tag, int version)
	{
		if (player.ClanTagVersion != version || !player.IsValid || !player.IsHuman)
			return;

		if (player.ClanTag != tag)
			player.ClanTag = tag;

		player.AppliedClanTag = tag;
	}
}
