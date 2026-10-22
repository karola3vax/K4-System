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
		string tag = string.Empty;
		bool hasTagSource = false;

		if (settings.RankTags && rank is not null)
		{
			tag = rank.Rank.Tag ?? $"[{rank.Rank.Name}]";
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
			tag = $"{settings.TopPlacement.Format.Replace("{placement}", rank.TopPlacement.ToString())} {tag}".TrimEnd();
			hasTagSource = true;
		}

		int version = ++player.ClanTagVersion;

		if (!settings.Country)
		{
			if (hasTagSource)
				SetTag(player, tag, version);
			else
				Release(player);
			return;
		}

		string? ip = player.Controller.IpAddress;
		countries.GetCountryCodeAsync(ip).ContinueWith(lookup =>
		{
			string country = lookup.IsCompletedSuccessfully ? lookup.Result : CountryLookup.Unknown;
			string complete = tag.Length > 0 ? $"{country} | {tag}" : country;
			gameThread.PostWorldUpdate(() => SetTag(player, complete, version));
		}, TaskScheduler.Default);
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
