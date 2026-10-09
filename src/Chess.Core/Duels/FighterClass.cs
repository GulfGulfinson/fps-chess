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
    float? ZoomedSpreadDegrees = null);

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
    private static readonly Dictionary<PieceType, FighterClass> Classes = new()
    {
        [PieceType.Pawn] = new(PieceType.Pawn, "Soldat", 100, 1f, 1f,
            new WeaponStats("Halbautomatik-Gewehr", 22, 0.25f, 12, 1.6f, 1.0f)),
        [PieceType.Knight] = new(PieceType.Knight, "Reiter", 120, 1f, 1f,
            new WeaponStats("Schrotflinte", 12, 0.9f, 6, 2.2f, 7f, Pellets: 8, MaxRange: 25f),
            DoubleJump: true),
        [PieceType.Bishop] = new(PieceType.Bishop, "Scharfschütze", 90, 1f, 1f,
            new WeaponStats("Scharfschützengewehr", 75, 1.4f, 5, 2.5f, 3f, ZoomedSpreadDegrees: 0f),
            ZoomFov: 30f),
        [PieceType.Rook] = new(PieceType.Rook, "Bollwerk", 200, 0.75f, 0.75f,
            new WeaponStats("Minigun", 9, 0.07f, 150, 3.5f, 2.5f, SpinUpSeconds: 0.8f)),
        [PieceType.Queen] = new(PieceType.Queen, "Kommandantin", 160, 1f, 1f,
            new WeaponStats("Sturmgewehr", 14, 0.1f, 30, 2.0f, 1.5f),
            Dash: new DashPerk(10f, 3f),
            Grenades: new GrenadePerk(2, 70f, 4f, 2f)),
        [PieceType.King] = new(PieceType.King, "Monarch", 140, 1f, 1f,
            new WeaponStats("Revolver", 45, 0.6f, 6, 2.4f, 0.5f),
            Regeneration: new RegenerationPerk(3f, 5f)),
    };

    public static FighterClass For(PieceType piece) => Classes[piece];
}
