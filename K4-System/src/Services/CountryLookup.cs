namespace K4System.Services;

using System.Collections.Concurrent;
using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;

/// <summary>Optional GeoIP country codes from GeoLite2-Country.mmdb beside the plugin. Unknown is "??".</summary>
internal sealed class CountryLookup(string moduleDirectory, ILogger logger) : IDisposable
{
	public const string Unknown = "??";
	public const string FileName = "GeoLite2-Country.mmdb";

	private readonly object gate = new();
	private readonly ConcurrentDictionary<string, Lazy<Task<string>>> cache = new(StringComparer.OrdinalIgnoreCase);
	private DatabaseReader? reader;
	private bool unavailableLogged;
	private bool disposed;

	public Task<string> GetCountryCodeAsync(string? playerIp)
	{
		string? ip = NormalizeIp(playerIp);
		if (ip is null)
			return Task.FromResult(Unknown);

		return cache.GetOrAdd(ip, key => new Lazy<Task<string>>(() => Task.Run(() => Resolve(key)), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
	}

	/// <summary>Accepts a bare IPv4/IPv6 address or an "address:port" endpoint.</summary>
	internal static string? NormalizeIp(string? playerIp)
	{
		if (string.IsNullOrWhiteSpace(playerIp))
			return null;

		string value = playerIp.Trim();
		if (IPAddress.TryParse(value, out IPAddress? address))
			return address.ToString();

		return IPEndPoint.TryParse(value, out IPEndPoint? endpoint) ? endpoint.Address.ToString() : null;
	}

	private string Resolve(string ip)
	{
		lock (gate)
		{
			try
			{
				DatabaseReader? database = GetReader();
				return database?.Country(ip).Country.IsoCode ?? Unknown;
			}
			catch (AddressNotFoundException)
			{
				return Unknown;
			}
			catch (Exception ex)
			{
				logger.LogError("Unable to resolve country for {PlayerIp}: {ErrorMessage}", ip, ex.Message);
				return Unknown;
			}
		}
	}

	private DatabaseReader? GetReader()
	{
		if (disposed)
			return null;

		if (reader is not null)
			return reader;

		string filePath = Path.Combine(moduleDirectory, FileName);
		if (!File.Exists(filePath))
		{
			LogUnavailableOnce($"{FileName} not found in {filePath}. Download it from https://github.com/P3TERX/GeoLite.mmdb/releases and place it in the same directory as the plugin.");
			return null;
		}

		try
		{
			reader = new DatabaseReader(filePath);
			return reader;
		}
		catch (Exception ex)
		{
			LogUnavailableOnce($"Unable to load {FileName}: {ex.Message}");
			return null;
		}
	}

	private void LogUnavailableOnce(string message)
	{
		if (unavailableLogged)
			return;

		unavailableLogged = true;
		logger.LogError("{Message}", message);
	}

	public void Dispose()
	{
		lock (gate)
		{
			disposed = true;
			reader?.Dispose();
			reader = null;
			cache.Clear();
		}
	}
}
