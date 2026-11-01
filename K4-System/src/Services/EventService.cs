namespace K4System.Services;

using K4System.Domain;
using K4System.Hud;
using K4System.Players;

/// <summary>
/// Points events: the schedule from the config and events admins start with css_k4_event. Announces each start
/// and end to everyone, in chat and as a toast with the panel. Game thread only.
/// </summary>
internal sealed class EventService(EventState state, PlayerRegistry players, Chat chat, HudBridge hud)
{
	public ActiveEvent? Current => state.Current(DateTime.UtcNow);

	/// <summary>The factor for positive game-event points right now; 1 without an event.</summary>
	public double Multiplier => state.Multiplier(DateTime.UtcNow);

	public void Start(double multiplier, int minutes)
	{
		state.Start(multiplier, minutes, DateTime.UtcNow);
		Tick();
	}

	public bool Stop()
	{
		if (!state.Stop(DateTime.UtcNow))
			return false;
		Tick();
		return true;
	}

	/// <summary>Announces events that started or ended. Runs on a timer, and right after an admin starts or stops one.</summary>
	public void Tick()
	{
		DateTime now = DateTime.UtcNow;
		(ActiveEvent? ended, ActiveEvent? started) = state.Advance(now);

		if (ended is ActiveEvent old && started is null)
			chat.TellAll("event.ended", PointEvents.Format(old.Multiplier));

		if (started is ActiveEvent active)
		{
			string multiplier = PointEvents.Format(active.Multiplier);
			int minutes = PointEvents.MinutesLeft(active, now);
			chat.TellAll("event.started", multiplier, minutes);

			if (hud.Hud is PanoramaHud panel)
			{
				foreach (K4Player player in players.ValidHumans.ToArray())
					panel.Toast(player, new Toast(chat.For(player.Controller, "hud.toast.event"), chat.For(player.Controller, "toast.event-text", minutes),
						$"{multiplier}×", "ic-trade_up", ToastColor.Gold, ToastLength.Long));
			}
		}

		if ((ended is not null || started is not null) && hud.Hud is PanoramaHud open)
			open.RefreshOpenPanels();
	}

	/// <summary>Tells a player who just joined that an event is on.</summary>
	public void Greet(K4Player player)
	{
		DateTime now = DateTime.UtcNow;
		if (state.Current(now) is ActiveEvent active && player.IsValid)
			chat.Tell(player.Controller, "event.running", PointEvents.Format(active.Multiplier), PointEvents.MinutesLeft(active, now));
	}
}
