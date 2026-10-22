namespace K4System.Missions;

using System.Net;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using K4System.Config;
using K4System.Hud;
using K4System.Players;
using K4System.Services;
using K4System.Storage;

/// <summary>What a kill event says about how the kill happened.</summary>
internal readonly record struct KillFacts(
	string Weapon,
	bool Headshot,
	bool Noscope,
	bool Wallbang,
	bool ThroughSmoke,
	bool Blind,
	bool InAir,
	float Distance,
	bool Revenge,
	bool Opening);

/// <summary>
/// Daily and weekly missions: assignment, progress, rewards, streaks, rerolls, and the !missions card.
/// Missions follow the map: one that cannot be done on the current map is swapped for free while it is untouched.
/// </summary>
internal sealed class MissionService(
	PluginConfig config,
	MissionPool pool,
	TimeZoneInfo zone,
	PlayerRegistry players,
	RankService ranks,
	GameState game,
	SqlPlayerStore store,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	HudBridge hud,
	Action<float, Action> retry)
{
	private const int CardSeconds = 8;

	private MissionSettings Settings => config.Missions;

	private DateOnly Today => MissionRules.Today(DateTime.UtcNow, zone);

	/// <summary>Progress counts under the same rules as stats: minimum players and warmup.</summary>
	public bool IsTrackingAllowed() => game.IsTrackingAllowed(config.Tracking.CountWarmup, config.Tracking.MinimumPlayers);

	// ---------------------------------------------------------------- the map

	private readonly HashSet<string> seenWeapons = new(StringComparer.Ordinal);
	private MapFacts? mapFacts;

	/// <summary>What the current map allows. Null until K4 has looked at it; until then no mission is swapped.</summary>
	public MapAbilities? Map { get; private set; }

	/// <summary>False only for a mission the current map cannot offer.</summary>
	public bool IsDoable(MissionDefinition mission) => Map?.Allows(mission) ?? true;

	public void OnMapStart()
	{
		Map = null;
		mapFacts = null;
		seenWeapons.Clear();
		roundKills.Clear();
		lifeKills.Clear();
		clutchers.Clear();
		healthBefore.Clear();
		OnMatchStart();
	}

	/// <summary>Freeze time ended: everyone holds their round's weapons, so this is when the map is read.</summary>
	public void OnFreezeEnd()
	{
		MapReader.AddCarriedWeapons(seenWeapons);
		ReadMap();
	}

	/// <summary>Fallback for modes without freeze time: reads the map if it was not read yet.</summary>
	public void ReadMapIfNeeded()
	{
		if (Map is null)
			ReadMap();
	}

	private void ReadMap()
	{
		mapFacts = MapReader.Read(config.Tracking.FreeForAll, seenWeapons);
		Map = MapAbilities.Read(mapFacts, Settings.MapRules);

		foreach (K4Player player in players.ValidHumans.ToArray())
			FitToMap(player);
	}

	/// <summary>A weapon seen in use. A new one can make more missions possible on this map.</summary>
	public void SawWeapon(string? weapon)
	{
		string name = WeaponCatalog.Normalize(weapon) switch
		{
			"inferno" => "molotov",
			string other => other
		};

		if (name.Length == 0 || !seenWeapons.Add(name) || mapFacts is null)
			return;

		Map = MapAbilities.Read(mapFacts, Settings.MapRules);
	}

	/// <summary>Swaps untouched missions the map cannot offer. Started ones stay and can be finished on another map.</summary>
	internal void FitToMap(K4Player player)
	{
		if (Map is not MapAbilities map || player.Missions is not PlayerMissions missions)
			return;

		EnsureToday(player);
		bool changed = false;

		for (int i = 0; i < missions.Slots.Count; i++)
		{
			if (Swapped(player, missions, missions.Slots[i], missions.Day, map) is MissionProgress replacement)
			{
				missions.Slots[i] = replacement;
				changed = true;
			}
		}

		if (missions.Weekly is MissionProgress weekly && Swapped(player, missions, weekly, missions.WeekStart, map) is MissionProgress newWeekly)
		{
			missions.Weekly = newWeekly;
			changed = true;
		}

		if (changed)
			(hud.Hud as PanoramaHud)?.Refresh(player, HudTab.Missions);
	}

	private MissionProgress? Swapped(K4Player player, PlayerMissions missions, MissionProgress slot, DateOnly day, MapAbilities map)
	{
		if (!slot.Untouched || map.Allows(slot.Mission))
			return null;

		MissionDefinition? replacement = MissionRules.Swap(pool, player.SteamId, day, slot.Mission, missions.All.Select(current => current.Mission.Id), map);
		if (replacement is null)
			return null;

		if (player.IsValid)
		{
			CCSPlayerController controller = player.Controller;
			chat.Tell(controller, "mission.swapped", chat.For(controller, DifficultyKey(replacement.Difficulty)), Describe(controller, replacement));
		}

		return new MissionProgress(replacement);
	}

	// ---------------------------------------------------------------- loading and day changes

	private const int FastLoadAttempts = 5;
	private const float LoadRetrySeconds = 15f;
	private const float SlowLoadRetrySeconds = 60f;

	// Match missions only count for players who were in the match for at least this share of it.
	private const double MinimumMatchShare = 0.5;
	private DateTime matchStartedUtc = DateTime.UtcNow;

	public void Load(K4Player player, int attempt = 1)
	{
		ulong steamId = player.SteamId;
		DateOnly day = Today;
		DateOnly week = MissionRules.WeekStart(day);

		background.Run(async () =>
		{
			MissionRecord record;
			try
			{
				record = await store.LoadMissionsAsync(steamId, MissionRules.FormatDay(day), MissionRules.FormatDay(week));
			}
			catch
			{
				// Progress is ignored until missions load, so a failed load must not be final.
				gameThread.Post(() => retry(attempt < FastLoadAttempts ? LoadRetrySeconds : SlowLoadRetrySeconds, () =>
				{
					if (players.Get(steamId) is { Missions: null } current)
						Load(current, attempt + 1);
				}));
				throw;
			}

			gameThread.Post(() =>
			{
				if (players.Get(steamId) is K4Player current && current.Missions is null)
				{
					current.Missions = Build(steamId, day, record);
					if (current.IsValid && current.Controller.TeamNum > 1)
						current.MatchJoinedUtc ??= DateTime.UtcNow;
					FitToMap(current);
				}
			});
		}, $"Loading missions for {steamId}");
	}

	/// <summary>Today's assignment and this week's mission, with any saved progress and rerolled missions laid over them.</summary>
	internal PlayerMissions Build(ulong steamId, DateOnly day, MissionRecord record)
	{
		List<MissionProgress> slots = MissionRules.Assign(pool, steamId, day, Map).Select(mission => new MissionProgress(mission)).ToList();

		foreach (MissionSlotSnapshot saved in record.Slots)
		{
			if (saved.Slot >= 0 && saved.Slot < slots.Count)
				slots[saved.Slot] = Restore(saved, slots[saved.Slot].Mission);
		}

		DateOnly week = MissionRules.WeekStart(day);
		MissionProgress? weekly = MissionRules.AssignWeekly(pool, steamId, week, Map) is MissionDefinition fresh ? new MissionProgress(fresh) : null;
		if (record.Weekly is MissionSlotSnapshot savedWeekly && weekly is not null)
			weekly = Restore(savedWeekly, weekly.Mission);

		return new PlayerMissions
		{
			Day = day,
			Slots = slots,
			WeekStart = week,
			Weekly = weekly,
			RerollsUsed = record.RerollsUsed,
			Streak = record.Streak,
			LastCompletedDay = MissionRules.ParseDay(record.LastCompletedDay)
		};
	}

	private MissionProgress Restore(MissionSlotSnapshot saved, MissionDefinition fresh)
	{
		if (pool.ById.TryGetValue(saved.MissionId, out MissionDefinition? mission))
			return new MissionProgress(mission, Math.Min(saved.Progress, mission.Target), saved.Completed);

		// The mission was removed from missions.jsonc after it was paid. Keep the slot done so it is not paid again.
		return saved.Completed ? new MissionProgress(fresh, fresh.Target, completed: true) : new MissionProgress(fresh);
	}

	/// <summary>Starts a new day's (and week's) missions when midnight passed while the player was online.</summary>
	internal void EnsureToday(K4Player player)
	{
		PlayerMissions? missions = player.Missions;
		DateOnly today = Today;

		if (missions is null || today <= missions.Day)
			return;

		missions.Day = today;
		missions.Slots = MissionRules.Assign(pool, player.SteamId, today, Map).Select(mission => new MissionProgress(mission)).ToList();
		missions.RerollsUsed = 0;

		DateOnly week = MissionRules.WeekStart(today);
		if (week > missions.WeekStart)
		{
			missions.WeekStart = week;
			missions.Weekly = MissionRules.AssignWeekly(pool, player.SteamId, week, Map) is MissionDefinition weekly ? new MissionProgress(weekly) : null;
		}
	}

	// ---------------------------------------------------------------- progress

	/// <summary>
	/// Adds progress to every unfinished mission of this type that matches. Missions that count the best try
	/// (kills in one round or one life) take the amount as that try instead. Callers check tracking rules first.
	/// </summary>
	public void Record(K4Player? player, MissionType type, int amount = 1, Func<MissionDefinition, bool>? matches = null)
	{
		if (player is not { IsHuman: true, Missions: not null } || amount <= 0)
			return;

		EnsureToday(player);
		PlayerMissions missions = player.Missions!;
		bool wasAllCompleted = missions.AllCompleted;

		foreach (MissionProgress slot in missions.All.ToArray())
		{
			if (slot.Completed || slot.Mission.Type != type || (matches is not null && !matches(slot.Mission)))
				continue;

			slot.Progress = slot.Mission.CountsBestTry
				? Math.Max(slot.Progress, Math.Min(amount, slot.Mission.Target))
				: (int)Math.Min((long)slot.Progress + amount, slot.Mission.Target);

			if (slot.Progress >= slot.Mission.Target)
				Complete(player, slot);
		}

		if (!wasAllCompleted && missions.AllCompleted)
			CompleteDay(player, missions);
	}

	internal int RewardOf(MissionDefinition mission) => mission.Reward ?? mission.Difficulty switch
	{
		MissionDifficulty.Easy => Settings.Rewards.Easy,
		MissionDifficulty.Medium => Settings.Rewards.Medium,
		MissionDifficulty.Weekly => Settings.Rewards.Weekly,
		_ => Settings.Rewards.Hard
	};

	private void Complete(K4Player player, MissionProgress slot)
	{
		slot.Completed = true;
		int reward = RewardOf(slot.Mission);
		ranks.GrantPoints(player, reward);

		CCSPlayerController controller = player.Controller;
		string description = Describe(controller, slot.Mission);

		if (hud.Hud is PanoramaHud panel)
		{
			panel.ShowToast(player, "mission.html-completed", $"{description} · +{reward}", ToastKind.Success);
			panel.Refresh(player, HudTab.Missions);
			return;
		}

		chat.Tell(controller, "mission.completed", description, reward);
		controller.PrintToCenterHtml(
			$"<font class='fontSize-l' color='#9AFF6E'>{Html(chat.For(controller, "mission.html-completed"))}</font><br>" +
			$"<font color='#FFFFFF'>{Html(description)}</font><br><font color='#9AFF6E'>+{reward}</font>", 4);
	}

	private void CompleteDay(K4Player player, PlayerMissions missions)
	{
		DateOnly today = missions.Day;

		// The streak bonus is paid once per day, even if a mission slot changes after it.
		if (missions.LastCompletedDay == today)
			return;

		missions.Streak = MissionRules.NextStreak(missions.Streak, missions.LastCompletedDay, today);
		missions.LastCompletedDay = today;

		int dayTotal = (int)Math.Min(int.MaxValue, missions.Slots.Sum(slot => (long)RewardOf(slot.Mission)));
		int bonus = MissionRules.StreakBonus(missions.Streak, dayTotal, Settings.StreakBonusPercent, Settings.StreakMaxDays);
		ranks.GrantPoints(player, bonus);

		if (hud.Hud is PanoramaHud panel)
			panel.ShowToast(player, "hud.toast.streak", chat.For(player.Controller, "hud.toast.streak-text", missions.Streak, bonus), ToastKind.Gold, "ic-hot_trend");
		else
			chat.Tell(player.Controller, "mission.all-completed", missions.Streak, bonus);
	}

	// ---------------------------------------------------------------- game events

	private static readonly MissionType[] GroupMissions =
		[MissionType.PistolKills, MissionType.SmgKills, MissionType.ShotgunKills, MissionType.RifleKills, MissionType.SniperKills];

	private readonly Dictionary<ulong, int> roundKills = [];
	private readonly Dictionary<ulong, int> lifeKills = [];
	private readonly Dictionary<ulong, int> clutchers = [];
	private readonly Dictionary<int, int> healthBefore = [];
	private bool openingTaken;

	public void OnRoundStart()
	{
		roundKills.Clear();
		clutchers.Clear();
		openingTaken = false;

		foreach (K4Player player in players.ValidHumans)
			EnsureToday(player);
	}

	/// <summary>True for the first death of the round, whoever died. Every death calls this once.</summary>
	public bool TakeOpening()
	{
		bool opening = !openingTaken;
		openingTaken = true;
		return opening;
	}

	/// <summary>The player died: their kills-in-one-life count starts again.</summary>
	public void OnLifeEnded(ulong steamId) => lifeKills.Remove(steamId);

	public void OnSpawn(int slot) => healthBefore.Remove(slot);

	/// <summary>A kill that already counts for points (not a team kill or suicide).</summary>
	public void OnKill(K4Player attacker, KillFacts kill)
	{
		string name = WeaponCatalog.Normalize(kill.Weapon);
		SawWeapon(name);

		Record(attacker, MissionType.Kills);
		Record(attacker, MissionType.WeaponKills, matches: mission => name.Contains(mission.Weapon!, StringComparison.Ordinal));

		if (kill.Headshot)
			Record(attacker, MissionType.Headshots);
		if (kill.Noscope)
			Record(attacker, MissionType.NoscopeKills);
		if (kill.Wallbang)
			Record(attacker, MissionType.WallbangKills);
		if (kill.ThroughSmoke)
			Record(attacker, MissionType.SmokeKills);
		if (kill.Blind)
			Record(attacker, MissionType.BlindKills);
		if (kill.InAir)
			Record(attacker, MissionType.JumpKills);
		if (kill.Revenge)
			Record(attacker, MissionType.RevengeKills);
		if (kill.Opening)
			Record(attacker, MissionType.OpeningKills);

		Record(attacker, MissionType.LongRangeKills, matches: mission => kill.Distance >= mission.MinimumDistance);

		if (WeaponCatalog.IsKnife(name))
			Record(attacker, MissionType.KnifeKills);
		else if (WeaponCatalog.IsGrenadeKill(name))
			Record(attacker, MissionType.GrenadeKills);
		else if (name == WeaponCatalog.Zeus)
			Record(attacker, MissionType.ZeusKills);

		foreach (MissionType group in GroupMissions)
		{
			if (WeaponCatalog.GroupOf(group)!.Contains(name))
				Record(attacker, group);
		}

		ulong steamId = attacker.SteamId;
		Record(attacker, MissionType.RoundKills, roundKills[steamId] = roundKills.GetValueOrDefault(steamId) + 1);
		Record(attacker, MissionType.LifeKills, lifeKills[steamId] = lifeKills.GetValueOrDefault(steamId) + 1);
	}

	public void OnAssist(K4Player assister, bool flash)
		=> Record(assister, flash ? MissionType.FlashAssists : MissionType.Assists);

	/// <summary>
	/// Every hit on a player, from anyone (fall damage, bots and teammates too), so the health before the next hit is known.
	/// Returns the health this hit really took: the killing hit can report more than the health that was left.
	/// </summary>
	public int TrackHealth(int victimSlot, int damage, int healthAfter)
	{
		int before = healthAfter > 0 ? healthAfter + damage : healthBefore.GetValueOrDefault(victimSlot, 100);
		healthBefore[victimSlot] = healthAfter;
		return Math.Clamp(before - healthAfter, 0, Math.Max(0, damage));
	}

	/// <summary>
	/// After a death: when it left one player alone on their team against living enemies, that player is in a
	/// clutch, and winning the round then counts. Only a teammate's death starts a clutch, so a player who was
	/// alone from the start of the round is not in one.
	/// </summary>
	public void CheckClutch(int victimTeam, int victimSlot)
	{
		if (victimTeam <= 1 || Map is { Teams: false } || config.Tracking.FreeForAll)
			return;

		List<CCSPlayerController> alive = Utilities.GetPlayers()
			.Where(controller => controller is { IsValid: true, PawnIsAlive: true } && controller.Slot != victimSlot && controller.TeamNum > 1)
			.ToList();

		List<CCSPlayerController> team = alive.Where(controller => controller.TeamNum == victimTeam).ToList();
		if (team.Count == 1 && alive.Count > 1 && players.Get(team[0]) is { IsHuman: true } last)
			clutchers.TryAdd(last.SteamId, victimTeam);
	}

	public void OnRoundEnd(int winnerTeam)
	{
		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			// Only players who were on this team for the whole round, so switching late does not count.
			if (player.Controller.TeamNum <= 1 || player.Rank?.PlayedRound != true)
				continue;

			int team = player.Controller.TeamNum;
			if (team == winnerTeam)
			{
				Record(player, MissionType.RoundWins);
				if (clutchers.TryGetValue(player.SteamId, out int clutchTeam) && clutchTeam == team)
					Record(player, MissionType.ClutchWins);
			}

			if (player.Controller.PawnIsAlive)
				Record(player, MissionType.SurviveRounds);
		}

		clutchers.Clear();
	}

	/// <summary>Called every minute: one minute for every active player on a team. AFK players do not count.</summary>
	public void OnPlaytimeMinute()
	{
		if (!IsTrackingAllowed())
			return;

		DateTime now = DateTime.UtcNow;
		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			bool active = now - player.LastActivityUtc <= TimeSpan.FromMinutes(2);
			if (player.Controller.TeamNum > 1 && active)
				Record(player, MissionType.PlaytimeMinutes);
		}
	}

	/// <summary>A new match (map start, warmup end, restart): presence is counted from now.</summary>
	public void OnMatchStart()
	{
		DateTime now = DateTime.UtcNow;
		matchStartedUtc = now;

		foreach (K4Player player in players.All)
			player.MatchJoinedUtc = player.IsValid && player.Controller.TeamNum > 1 ? now : null;
	}

	public void OnTeamJoined(K4Player player, int team)
	{
		if (team > 1)
			player.MatchJoinedUtc ??= DateTime.UtcNow;
	}

	public void OnMatchEnd()
	{
		if (!IsTrackingAllowed())
			return;

		DateTime now = DateTime.UtcNow;
		TimeSpan required = (now - matchStartedUtc) * MinimumMatchShare;

		(List<K4Player> played, HashSet<K4Player> winners, _) = MatchResults.Get(players, config.Tracking.FreeForAll);
		foreach (K4Player player in played)
		{
			// Joining (or switching to the winning team) at the very end does not count as playing the match.
			if (player.MatchJoinedUtc is not DateTime joined || now - joined < required)
				continue;

			Record(player, MissionType.MatchesPlayed);
			if (winners.Contains(player))
				Record(player, MissionType.MatchWins);
		}
	}

	// ---------------------------------------------------------------- !missions

	public void Show(K4Player player, string commandName)
	{
		if (player.Missions is not PlayerMissions missions)
		{
			chat.Tell(player.Controller, "error.loading");
			return;
		}

		EnsureToday(player);
		CCSPlayerController controller = player.Controller;
		DateOnly today = missions.Day;
		int streak = MissionRules.CurrentStreak(missions.Streak, missions.LastCompletedDay, today);
		string resetIn = MissionRules.FormatDuration(MissionRules.UntilReset(DateTime.UtcNow, zone));

		chat.Tell(controller, "mission.title");
		List<MissionProgress> all = missions.All.ToList();
		for (int i = 0; i < all.Count; i++)
		{
			MissionProgress slot = all[i];
			string difficulty = DifficultyLabel(controller, slot.Mission);
			string description = DescribeHere(controller, slot);

			if (slot.Completed)
				chat.TellLine(controller, "mission.line-done", difficulty, description, RewardOf(slot.Mission));
			else
				chat.TellLine(controller, "mission.line", i + 1, difficulty, description, slot.Progress, slot.Mission.Target, RewardOf(slot.Mission), MissionRules.ProgressBar(slot.Progress, slot.Mission.Target));
		}

		chat.TellLine(controller, "mission.footer", streak, resetIn);
		if (missions.RerollsUsed < Settings.RerollsPerDay && missions.Slots.Any(slot => !slot.Completed && slot.Progress == 0))
			chat.TellLine(controller, "mission.reroll-hint", commandName);

		controller.PrintToCenterHtml(Card(controller, missions, streak, resetIn), CardSeconds);
	}

	private string Card(CCSPlayerController controller, PlayerMissions missions, int streak, string resetIn)
	{
		StringBuilder html = new();
		html.Append($"<font class='fontSize-l' color='#9AFF6E'>{Html(chat.For(controller, "mission.html-title"))}</font><br>");

		foreach (MissionProgress slot in missions.All)
		{
			string color = slot.Mission.Difficulty switch
			{
				MissionDifficulty.Easy => "#7EE787",
				MissionDifficulty.Medium => "#F2CC60",
				MissionDifficulty.Weekly => "#C99BFF",
				_ => "#FF7B72"
			};

			html.Append($"<font color='{color}'>{Html(DifficultyLabel(controller, slot.Mission))}</font> ");
			html.Append($"<font color='#FFFFFF'>{Html(DescribeHere(controller, slot))}</font>");

			// The weekly mission takes one line, so the card is only one line taller than before.
			if (slot.Mission.Difficulty == MissionDifficulty.Weekly)
			{
				html.Append(slot.Completed
					? $" <font color='#9AFF6E'>✔ +{RewardOf(slot.Mission)}</font><br>"
					: $" <font color='#C0C0C0'>{slot.Progress}/{slot.Mission.Target} · +{RewardOf(slot.Mission)}</font><br>");
				continue;
			}

			html.Append("<br>");
			if (slot.Completed)
				html.Append($"<font color='#9AFF6E'>✔ +{RewardOf(slot.Mission)}</font><br>");
			else
				html.Append($"<font color='#9AFF6E'>{MissionRules.ProgressBar(slot.Progress, slot.Mission.Target)}</font> <font color='#C0C0C0'>{slot.Progress}/{slot.Mission.Target} · +{RewardOf(slot.Mission)}</font><br>");
		}

		html.Append($"<font class='fontSize-s' color='#C0C0C0'>{Html(chat.For(controller, "mission.html-footer", streak, resetIn))}</font>");
		return html.ToString();
	}

	/// <summary>"Streak 4 · new missions in 5h 07m", in the player's language.</summary>
	internal string Footer(K4Player player)
	{
		PlayerMissions missions = player.Missions!;
		int streak = MissionRules.CurrentStreak(missions.Streak, missions.LastCompletedDay, missions.Day);
		string resetIn = MissionRules.FormatDuration(MissionRules.UntilReset(DateTime.UtcNow, zone));
		return chat.For(player.Controller, "mission.html-footer", streak, resetIn);
	}

	public void Reroll(K4Player player, string? slotArgument)
	{
		CCSPlayerController controller = player.Controller;

		if (player.Missions is not PlayerMissions missions)
		{
			chat.Tell(controller, "error.loading");
			return;
		}

		EnsureToday(player);

		if (missions.RerollsUsed >= Settings.RerollsPerDay)
		{
			chat.Tell(controller, "mission.reroll-used");
			return;
		}

		if (!int.TryParse(slotArgument, out int number) || number < 1 || number > missions.Slots.Count
			|| missions.Slots[number - 1] is not { Completed: false, Progress: 0 } slot)
		{
			chat.Tell(controller, "mission.reroll-invalid");
			return;
		}

		MissionDefinition? replacement = MissionRules.Reroll(pool, player.SteamId, missions.Day, slot.Mission, missions.All.Select(current => current.Mission.Id), Map);
		if (replacement is null)
		{
			chat.Tell(controller, "mission.reroll-none");
			return;
		}

		missions.Slots[number - 1] = new MissionProgress(replacement);
		missions.RerollsUsed++;
		chat.Tell(controller, "mission.rerolled", number, Describe(controller, replacement));
		(hud.Hud as PanoramaHud)?.Refresh(player, HudTab.Missions);
	}

	// ---------------------------------------------------------------- text

	internal static string DifficultyKey(MissionDifficulty difficulty) => difficulty switch
	{
		MissionDifficulty.Easy => "mission.easy",
		MissionDifficulty.Medium => "mission.medium",
		MissionDifficulty.Weekly => "mission.weekly",
		_ => "mission.hard"
	};

	/// <summary>"Hard", or "Weekly (3d 04h)" with the time until the weekly mission changes.</summary>
	internal string DifficultyLabel(CCSPlayerController controller, MissionDefinition mission)
	{
		string label = chat.For(controller, DifficultyKey(mission.Difficulty));
		return mission.Difficulty == MissionDifficulty.Weekly
			? $"{label} ({MissionRules.FormatDuration(MissionRules.UntilWeeklyReset(DateTime.UtcNow, zone))})"
			: label;
	}

	internal string Describe(CCSPlayerController controller, MissionDefinition mission)
		=> chat.For(controller, mission.DescriptionKey, mission.Target, DescriptionDetail(mission));

	/// <summary>The description, marked when the mission cannot be done on the current map.</summary>
	internal string DescribeHere(CCSPlayerController controller, MissionProgress slot)
	{
		string description = Describe(controller, slot.Mission);
		return slot.Completed || IsDoable(slot.Mission) ? description : $"{description} ({chat.For(controller, "mission.unavailable")})";
	}

	/// <summary>The second value in a mission description: the weapon, or the distance in meters.</summary>
	private static string DescriptionDetail(MissionDefinition mission) => mission.Type == MissionType.LongRangeKills
		? mission.MinimumDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
		: WeaponName(mission.Weapon);

	internal static string WeaponName(string? weapon) => weapon switch
	{
		null => string.Empty,
		"ak47" => "AK-47",
		"m4a1" => "M4",
		"m4a1_silencer" => "M4A1-S",
		"awp" => "AWP",
		"deagle" => "Desert Eagle",
		"usp_silencer" => "USP-S",
		"glock" => "Glock-18",
		"ssg08" => "SSG 08",
		"mp9" => "MP9",
		"p90" => "P90",
		_ => weapon.ToUpperInvariant()
	};

	private static string Html(string text) => WebUtility.HtmlEncode(text);
}
