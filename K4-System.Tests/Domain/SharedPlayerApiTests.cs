namespace K4System.Tests.Domain;

using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using K4SharedApi;
using K4System.Api;

public class SharedPlayerApiTests
{
	[Fact]
	public void SharedApiProviderReplacementRemovesStaleProviders()
	{
		PlayerCapability<IPlayerAPI> capability = SharedPlayerApi.Capability;
		Func<CCSPlayerController, IPlayerAPI> staleProvider = _ => new DummyPlayerApi("stale");

		Capabilities.RegisterPlayerCapability(capability, staleProvider);
		Capabilities.RegisterPlayerCapability(capability, staleProvider);

		Assert.True(SharedPlayerApi.TryReplaceProviders(out string? error), error);

		List<Func<CCSPlayerController, IPlayerAPI>> providers = ProviderMap()[capability.Name];
		Assert.Single(providers);
		Assert.NotSame(staleProvider, providers[0]);

		// No plugin source is registered in tests, so the K4 provider answers with the unavailable API.
		IPlayerAPI? api = capability.Get(null!);
		Assert.IsType<UnavailablePlayerApi>(api);
		Assert.False(api.IsLoaded);
	}

	private static Dictionary<string, List<Func<CCSPlayerController, IPlayerAPI>>> ProviderMap()
	{
		FieldInfo? field = typeof(PlayerCapability<IPlayerAPI>).GetField("Providers", BindingFlags.NonPublic | BindingFlags.Static);
		return Assert.IsType<Dictionary<string, List<Func<CCSPlayerController, IPlayerAPI>>>>(field?.GetValue(null));
	}

	private sealed class DummyPlayerApi(string name) : IPlayerAPI
	{
		public string Name { get; } = name;
		public int TopPlacement => 0;
		public bool IsLoaded => true;
		public bool IsValid => true;
		public bool IsPlayer => true;
		public CCSPlayerController Controller => null!;
		public int Points { get; set; }
		public int RankID => 0;
		public string RankName => Name;
		public string RankClanTag => Name;
	}
}
