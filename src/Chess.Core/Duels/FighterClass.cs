namespace Chess.Core.Duels;

/// <param name="Pellets">Projectiles per shot; each pellet deals <paramref name="Damage"/>.</param>
/// <param name="SpinUpSeconds">Time the trigger must be held before the first shot (minigun).</param>
/// <param name="ZoomedSpreadDegrees">Spread while zoomed, for weapons with a scope.</param>
public sealed record WeaponStats(
    string Name,
    float Damage,
    float FireInterval,
    int Magazine,
    float ReloadSeconds,
    float SpreadDegrees,
    int Pellets = 1,
    float MaxRange = 300f,
    float SpinUpSeconds = 0f,
    float? ZoomedSpreadDegrees = null)
{
    /// <summary>Aiming down the sights halves the spread; a scope removes it entirely.</summary>
    public float SpreadFor(bool aiming) => aiming ? ZoomedSpreadDegrees ?? SpreadDegrees / 2f : SpreadDegrees;
}

public sealed record DashPerk(float Distance, float Cooldown);

public sealed record RegenerationPerk(float DelaySeconds, float HealthPerSecond);

public sealed record GrenadePerk(int Count, float Damage, float Radius, float FuseSeconds)
{
    public float DamageAt(float distance) => distance >= Radius ? 0f : Damage * (1f - distance / Radius);
}

/// <summary>What a piece fights with in a duel. All balancing numbers live in <see cref="FighterClasses"/>.</summary>
public sealed record FighterClass(
    PieceType Piece,
    string Name,
    float MaxHealth,
    float SpeedMultiplier,
    float DamageTakenMultiplier,
    WeaponStats Weapon,
    bool DoubleJump = false,
    float? ZoomFov = null,
    DashPerk? Dash = null,
    GrenadePerk? Grenades = null,
    RegenerationPerk? Regeneration = null);

public static class FighterClasses
{
    // Balanced to a band (see ClassesStayWithinTheBalanceBand): 100 to 150 health, 55 to 110 damage per second,
    // so the roles differ but every piece can win a duel.
    private static readonly Dictionary<PieceType, FighterClass> Classes = new()
    {
        [PieceType.Pawn] = new(PieceType.Pawn, "Soldat", 110, 1f, 1f,
            new WeaponStats("Halbautomatik-Gewehr", 18, 0.2f, 15, 1.6f, 1.0f)),
        [PieceType.Knight] = new(PieceType.Knight, "Reiter", 120, 1f, 1f,
            new WeaponStats("Schrotflinte", 11, 0.85f, 6, 2.2f, 6f, Pellets: 8, MaxRange: 20f),
            DoubleJump: true),
        [PieceType.Bishop] = new(PieceType.Bishop, "Scharfschütze", 100, 1f, 1f,
            new WeaponStats("Scharfschützengewehr", 80, 1.25f, 5, 2.4f, 3f, ZoomedSpreadDegrees: 0f),
            ZoomFov: 30f),
        [PieceType.Rook] = new(PieceType.Rook, "Bollwerk", 150, 0.8f, 0.85f,
            new WeaponStats("Minigun", 7, 0.075f, 150, 3.5f, 2.5f, SpinUpSeconds: 0.8f)),
        [PieceType.Queen] = new(PieceType.Queen, "Kommandantin", 140, 1f, 1f,
            new WeaponStats("Sturmgewehr", 11, 0.11f, 30, 2.0f, 1.5f),
            Dash: new DashPerk(10f, 3f),
            Grenades: new GrenadePerk(2, 60f, 4f, 2f)),
        [PieceType.King] = new(PieceType.King, "Monarch", 130, 1f, 1f,
            new WeaponStats("Revolver", 40, 0.55f, 6, 2.4f, 0.5f),
            Regeneration: new RegenerationPerk(2.5f, 6f)),
    };

    public static FighterClass For(PieceType piece) => Classes[piece];

    /// <summary>Hits on the top of a piece (its "head") deal extra damage.</summary>
    public const float HeadshotMultiplier = 1.5f;

    /// <summary>Relative height (0 feet, 1 top) from which a hit counts as a headshot.</summary>
    public const float HeadZone = 0.82f;

    public static bool IsHeadshot(float relativeHeight) => relativeHeight >= HeadZone;
}
