namespace K4System.Tests.Domain;

using K4System.Domain;

public class DeathRulesTests
{
	private static readonly DeathActor HumanT = new(1, 76561198000000001, true, 2);
	private static readonly DeathActor HumanT2 = new(2, 76561198000000002, true, 2);
	private static readonly DeathActor HumanCt = new(3, 76561198000000003, true, 3);
	private static readonly DeathActor BotCt = new(4, 0, false, 3);
	private static readonly DeathActor BotT = new(5, 0, false, 2);

	private sealed record HelperScenario(
		DeathActor? Victim,
		DeathActor? Attacker,
		bool IncludeBots,
		bool FfaMode,
		VictimPointAction VictimPoint,
		AttackerPointAction AttackerPoint,
		bool CountVictim,
		bool CountAttacker);

	private static readonly Dictionary<string, HelperScenario> HelperScenarios = new()
	{
		["human kills bot with bot tracking"] = new(BotCt, HumanT, true, true, VictimPointAction.None, AttackerPointAction.Kill, true, true),
		["human kills bot without bot tracking"] = new(BotCt, HumanT, false, true, VictimPointAction.None, AttackerPointAction.None, false, false),
		["bot kills human with bot tracking"] = new(HumanT, BotCt, true, true, VictimPointAction.Death, AttackerPointAction.None, true, false),
		["world kills human"] = new(HumanT, null, true, true, VictimPointAction.Suicide, AttackerPointAction.None, true, false),
		["human kills self"] = new(HumanT, HumanT, true, true, VictimPointAction.Suicide, AttackerPointAction.None, true, false),
		["same team human kill in team mode"] = new(HumanT, HumanT2, true, false, VictimPointAction.Death, AttackerPointAction.TeamKill, true, true),
		["same team human kill in ffa mode"] = new(HumanT, HumanT2, true, true, VictimPointAction.Death, AttackerPointAction.Kill, true, true),
		["human kill against bot is modifier eligible"] = new(BotT, HumanCt, true, false, VictimPointAction.None, AttackerPointAction.Kill, true, true)
	};

	public static TheoryData<string> HelperScenarioNames()
	{
		TheoryData<string> names = [];
		foreach (string name in HelperScenarios.Keys)
			names.Add(name);
		return names;
	}

	[Theory]
	[MemberData(nameof(HelperScenarioNames))]
	public void BotDeathHelperRegressionScenario(string name)
	{
		HelperScenario scenario = HelperScenarios[name];
		DeathDecision decision = DeathRules.Classify(scenario.Victim, scenario.Attacker, null, scenario.IncludeBots, scenario.FfaMode);

		Assert.Equal(scenario.VictimPoint, decision.VictimPoint);
		Assert.Equal(scenario.AttackerPoint, decision.AttackerPoint);
		Assert.Equal(scenario.CountVictim, decision.CountVictim);
		Assert.Equal(scenario.CountAttacker, decision.CountAttacker);
		Assert.False(decision.CountAssister);
	}

	public static TheoryData<string> EvalCaseNames()
	{
		TheoryData<string> names = [];
		foreach (BotDeathEvalCase evalCase in TestData.LoadEvalList<BotDeathEvalCase>("bot-death-events.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EvalCaseNames))]
	public void BotDeathEvalScenario(string name)
	{
		BotDeathEvalCase evalCase = TestData.LoadEvalList<BotDeathEvalCase>("bot-death-events.json").Single(item => item.Name == name);

		DeathDecision decision = DeathRules.Classify(
			ToActor(evalCase.Victim),
			ToActor(evalCase.Attacker),
			ToActor(evalCase.Assister),
			evalCase.IncludeBots,
			evalCase.FfaMode);

		Assert.Equal(evalCase.ExpectedVictimPoint, decision.VictimPoint.ToString());
		Assert.Equal(evalCase.ExpectedAttackerPoint, decision.AttackerPoint.ToString());
		Assert.Equal(evalCase.ExpectedCountVictimStats, decision.CountVictim);
		Assert.Equal(evalCase.ExpectedCountAttackerStats, decision.CountAttacker);
		Assert.Equal(evalCase.ExpectedCountAssister, decision.CountAssister);
		Assert.Equal(evalCase.ExpectedAssisterVictimIsEnemy, decision.AssisterVictimIsEnemy);
	}

	private static DeathActor? ToActor(BotDeathEvalActor? actor)
		=> actor is null ? null : new DeathActor(actor.Slot, actor.SteamId, actor.IsHuman, actor.Team);

	public sealed class BotDeathEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public BotDeathEvalActor? Victim { get; set; }
		public BotDeathEvalActor? Attacker { get; set; }
		public BotDeathEvalActor? Assister { get; set; }
		public bool IncludeBots { get; set; }
		public bool FfaMode { get; set; }
		public string ExpectedVictimPoint { get; set; } = string.Empty;
		public string ExpectedAttackerPoint { get; set; } = string.Empty;
		public bool ExpectedCountVictimStats { get; set; }
		public bool ExpectedCountAttackerStats { get; set; }
		public bool ExpectedCountAssister { get; set; }
		public bool ExpectedAssisterVictimIsEnemy { get; set; }
	}

	public sealed class BotDeathEvalActor
	{
		public int Slot { get; set; }
		public ulong SteamId { get; set; }
		public bool IsHuman { get; set; }
		public int Team { get; set; }
	}
}
