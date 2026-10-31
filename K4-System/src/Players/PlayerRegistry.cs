namespace K4System.Players;

using CounterStrikeSharp.API.Core;

/// <summary>
/// Loaded and loading players, keyed by slot. Game thread only, so there are no locks and no
/// copying of the whole list for every lookup.
/// </summary>
public sealed class PlayerRegistry
{
	private readonly Dictionary<int, K4Player> loaded = [];
	private readonly Dictionary<int, K4Player> loading = [];

	// A copy of the loaded players for other plugins' threads, replaced (never changed) on the game thread.
	private volatile K4Player[] snapshot = [];

	private void Publish() => snapshot = [.. loaded.Values];

	/// <summary>
	/// The loaded player for this controller, from any thread. Compares entity handles only and reads nothing
	/// from the game, so it is safe off the game thread; the answer may be one frame old.
	/// </summary>
	public K4Player? FindFromAnyThread(CCSPlayerController? controller)
	{
		if (controller is null)
			return null;

		foreach (K4Player player in snapshot)
		{
			if (player.Controller.Handle == controller.Handle)
				return player;
		}

		return null;
	}

	public IEnumerable<K4Player> All => loaded.Values;

	/// <summary>Loaded humans whose controller and pawn are valid.</summary>
	public IEnumerable<K4Player> ValidHumans => loaded.Values.Where(player => player.IsHuman && player.IsValid);

	public int Count => loaded.Count;

	// Compares entity handles only, so it is safe even for a controller that is being destroyed.
	public K4Player? Get(CCSPlayerController? controller)
	{
		if (controller is null)
			return null;

		foreach (K4Player player in loaded.Values)
		{
			if (player.Matches(controller))
				return player;
		}

		return null;
	}

	public K4Player? Get(ulong steamId)
	{
		if (steamId == 0)
			return null;

		foreach (K4Player player in loaded.Values)
		{
			if (player.SteamId == steamId)
				return player;
		}

		return null;
	}

	public bool IsLoaded(CCSPlayerController controller) => Get(controller) is not null;

	/// <summary>
	/// Adds a player, replacing any older entry for the same slot or SteamID. Permissions K4 granted
	/// to an older entry of the same player move to the new one, so they can still be removed later.
	/// Returns replaced entries that belonged to other players; the caller must release them.
	/// </summary>
	public List<K4Player> Add(K4Player player)
	{
		List<K4Player> displaced = [];

		foreach ((int slot, K4Player existing) in loaded.ToArray())
		{
			if (ReferenceEquals(existing, player))
				continue;

			bool sameSlot = slot == player.Slot;
			bool sameSteamId = player.SteamId != 0 && existing.SteamId == player.SteamId;

			if (!sameSlot && !sameSteamId)
				continue;

			if (sameSteamId)
				player.OwnedPermissions.UnionWith(existing.OwnedPermissions);
			else
				displaced.Add(existing);

			loaded.Remove(slot);
		}

		loaded[player.Slot] = player;
		Publish();
		return displaced;
	}

	public void Remove(K4Player player)
	{
		if (loaded.TryGetValue(player.Slot, out K4Player? current) && ReferenceEquals(current, player))
		{
			loaded.Remove(player.Slot);
			Publish();
		}
	}

	/// <summary>Entries whose controller is gone, for example players who left during a map change.</summary>
	public List<K4Player> Disconnected()
		=> loaded.Values.Where(player => !player.HasController || player.Controller.Connected != PlayerConnectedState.Connected).ToList();

	public K4Player? GetLoading(CCSPlayerController? controller)
	{
		if (controller is null)
			return null;

		foreach (K4Player player in loading.Values)
		{
			if (player.Matches(controller))
				return player;
		}

		return null;
	}

	public void BeginLoading(K4Player player) => loading[player.Slot] = player;

	public bool IsLoading(K4Player player)
		=> loading.TryGetValue(player.Slot, out K4Player? current) && ReferenceEquals(current, player);

	public void EndLoading(K4Player player)
	{
		if (IsLoading(player))
			loading.Remove(player.Slot);
	}

	public void Clear()
	{
		loaded.Clear();
		loading.Clear();
		Publish();
	}
}
