namespace K4System.Domain;

internal enum VictimPointAction
{
	None,
	Death,
	Suicide
}

internal enum AttackerPointAction
{
	None,
	Kill,
	TeamKill
}

internal readonly record struct DeathActor(int Slot, ulong SteamId, bool IsHuman, int Team);

internal readonly record struct DeathDecision(
	VictimPointAction VictimPoint,
	AttackerPointAction AttackerPoint,
	bool CountVictim,
	bool CountAttacker,
	bool CountAssister,
	bool AssisterVictimIsEnemy);

/// <summary>Decides who is counted for one death. Points and stats use their own bot settings.</summary>
internal static class DeathRules
{
	public static DeathDecision Classify(DeathActor? victim, DeathActor? attacker, DeathActor? assister, bool includeBots, bool ffaMode)
	{
		if (!victim.HasValue)
			return default;

		DeathActor victimActor = victim.Value;
		bool victimIncluded = victimActor.IsHuman || includeBots;
		bool attackerIsDifferent = attacker.HasValue && !IsSameActor(attacker.Value, victimActor);
		VictimPointAction victimPoint = VictimPointAction.None;

		if (victimActor.IsHuman)
		{
			if (!attacker.HasValue || !attackerIsDifferent)
				victimPoint = VictimPointAction.Suicide;
			else if (attacker.Value.IsHuman || includeBots)
				victimPoint = VictimPointAction.Death;
		}

		AttackerPointAction attackerPoint = AttackerPointAction.None;
		bool countAttacker = false;

		if (attacker.HasValue && attacker.Value.IsHuman && attackerIsDifferent && victimIncluded)
		{
			attackerPoint = !ffaMode && attacker.Value.Team == victimActor.Team ? AttackerPointAction.TeamKill : AttackerPointAction.Kill;
			countAttacker = true;
		}

		bool countAssister = assister.HasValue && assister.Value.IsHuman && victimIncluded;
		bool assisterVictimIsEnemy = ffaMode || (assister.HasValue && assister.Value.Team != victimActor.Team);

		return new DeathDecision(victimPoint, attackerPoint, victimIncluded, countAttacker, countAssister, assisterVictimIsEnemy);
	}

	public static bool IsSameActor(DeathActor left, DeathActor right)
		=> left.Slot == right.Slot || (left.SteamId != 0 && left.SteamId == right.SteamId);
}
