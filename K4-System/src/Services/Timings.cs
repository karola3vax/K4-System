namespace K4System.Services;

using System.Diagnostics;
using System.Globalization;

/// <summary>
/// How long K4's game-thread work takes, per event, for css_k4_hud. Server owners reported slow frames;
/// this shows whether K4 is the cause without a profiler. Game thread only.
/// </summary>
internal sealed class Timings
{
	private readonly Dictionary<string, (int Count, long Ticks, long MaxTicks)> sections = new(StringComparer.Ordinal);

	public T Time<T>(string name, Func<T> work)
	{
		long started = Stopwatch.GetTimestamp();
		try
		{
			return work();
		}
		finally
		{
			Record(name, Stopwatch.GetTimestamp() - started);
		}
	}

	public void Record(string name, long ticks)
	{
		(int count, long total, long max) = sections.GetValueOrDefault(name);
		sections[name] = (count + 1, total + ticks, Math.Max(max, ticks));
	}

	public IEnumerable<string> Describe()
	{
		foreach ((string name, (int count, long ticks, long max)) in sections.OrderByDescending(entry => entry.Value.MaxTicks))
			yield return string.Create(CultureInfo.InvariantCulture, $"{name}: {count}x, average {Ms(ticks) / count:0.000} ms, slowest {Ms(max):0.00} ms");
	}

	private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
