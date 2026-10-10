namespace Chess.Core.Duels;

/// <summary>
/// Rough chance that an attacking piece wins its duel, from the class table alone. A fighter wins if it
/// needs less time to empty the other's health, so the strength of a class is effective health times
/// damage per second. The defender gets a small edge because it wins when the time runs out.
/// </summary>
public static class DuelOdds
{
    private const float DefenderEdge = 0.9f;

    public static float AttackerWinProbability(PieceType attacker, PieceType defender)
    {
        var a = Strength(attacker) * DefenderEdge;
        var d = Strength(defender);
        // Aim matters more than the numbers, so no matchup is ever treated as certain.
        return Math.Clamp(a / (a + d), 0.15f, 0.85f);
    }

    private static float Strength(PieceType type)
    {
        var fighter = FighterClasses.For(type);
        var weapon = fighter.Weapon;
        return fighter.MaxHealth / fighter.DamageTakenMultiplier * weapon.Damage * weapon.Pellets / weapon.FireInterval;
    }
}
