using Chess.Core;
using Chess.Core.Duels;

namespace Chess.Core.Tests;

public class DuelStateTests
{
    /// <summary>A duel that is past its countdown.</summary>
    private static DuelState Fighting(PieceType attacker = PieceType.Pawn, PieceType defender = PieceType.Pawn)
    {
        var duel = new DuelState(attacker, defender);
        duel.Tick(DuelState.CountdownSeconds);
        return duel;
    }

    [Fact]
    public void FightersStartWithTheirClassStats()
    {
        var duel = new DuelState(PieceType.Rook, PieceType.Bishop);

        Assert.Equal(150, duel.Attacker.Health);
        Assert.Equal(150, duel.Attacker.Ammo);
        Assert.Equal(100, duel.Defender.Health);
        Assert.Equal(DuelPhase.Countdown, duel.Phase);
    }

    [Fact]
    public void NobodyCanShootDuringTheCountdown()
    {
        var duel = new DuelState(PieceType.Pawn, PieceType.Pawn);

        Assert.False(duel.TryFire(DuelRole.Attacker));
        duel.Tick(DuelState.CountdownSeconds);
        Assert.Equal(DuelPhase.Fighting, duel.Phase);
        Assert.True(duel.TryFire(DuelRole.Attacker));
    }

    [Fact]
    public void FireRateAndAmmoAreEnforced()
    {
        var duel = Fighting();

        Assert.True(duel.TryFire(DuelRole.Attacker));
        Assert.False(duel.TryFire(DuelRole.Attacker));
        duel.Tick(0.2f);
        Assert.True(duel.TryFire(DuelRole.Attacker));
        Assert.Equal(13, duel.Attacker.Ammo);
    }

    [Fact]
    public void ToleranceAcceptsShotsJustBeforeTheCooldownEnds()
    {
        var duel = Fighting();
        duel.TryFire(DuelRole.Attacker);
        duel.Tick(0.17f);

        Assert.False(duel.TryFire(DuelRole.Attacker));
        Assert.True(duel.TryFire(DuelRole.Attacker, tolerance: 0.05f));
    }

    [Fact]
    public void EmptyMagazineReloadsAutomatically()
    {
        var duel = Fighting();
        for (var i = 0; i < 15; i++)
        {
            Assert.True(duel.TryFire(DuelRole.Attacker));
            duel.Tick(0.2f);
        }

        Assert.False(duel.TryFire(DuelRole.Attacker));
        Assert.True(duel.Attacker.IsReloading);
        duel.Tick(1.6f);
        Assert.Equal(15, duel.Attacker.Ammo);
        Assert.True(duel.TryFire(DuelRole.Attacker));
    }

    [Fact]
    public void ManualReloadOnlyWithMissingAmmo()
    {
        var duel = Fighting();

        Assert.False(duel.StartReload(DuelRole.Attacker));
        duel.TryFire(DuelRole.Attacker);
        Assert.True(duel.StartReload(DuelRole.Attacker));
        Assert.False(duel.TryFire(DuelRole.Attacker));
    }

    [Fact]
    public void DamageKillsAndDecidesTheDuel()
    {
        var duel = Fighting();

        duel.ApplyDamage(DuelRole.Defender, 60);
        Assert.Null(duel.Outcome);
        duel.ApplyDamage(DuelRole.Defender, 60);

        Assert.Equal(0, duel.Defender.Health);
        Assert.Equal(DuelOutcome.AttackerWon, duel.Outcome);
        Assert.Equal(DuelPhase.Over, duel.Phase);
        Assert.False(duel.TryFire(DuelRole.Attacker));
    }

    [Fact]
    public void RookArmorReducesDamage()
    {
        var duel = Fighting(PieceType.Pawn, PieceType.Rook);

        var applied = duel.ApplyDamage(DuelRole.Defender, 100);

        Assert.Equal(85, applied, 3);
        Assert.Equal(65, duel.Defender.Health, 3);
    }

    [Fact]
    public void DefenderWinsOnTimeout()
    {
        var duel = Fighting();

        duel.Tick(DuelState.TimeLimitSeconds - 0.1f);
        Assert.Null(duel.Outcome);
        duel.Tick(0.2f);

        Assert.Equal(DuelOutcome.DefenderWon, duel.Outcome);
    }

    [Fact]
    public void FallingOffTheTableLoses()
    {
        var duel = Fighting();

        duel.Fall(DuelRole.Defender);

        Assert.Equal(DuelOutcome.AttackerWon, duel.Outcome);
    }

    [Fact]
    public void KingRegeneratesAfterNotBeingHit()
    {
        var duel = Fighting(PieceType.Pawn, PieceType.King);
        duel.ApplyDamage(DuelRole.Defender, 40);

        duel.Tick(2.4f);
        Assert.Equal(90, duel.Defender.Health, 3);
        duel.Tick(2.1f);

        Assert.Equal(102, duel.Defender.Health, 1);
    }

    [Fact]
    public void RegenerationNeverExceedsMaximum()
    {
        var duel = Fighting(PieceType.Pawn, PieceType.King);
        duel.ApplyDamage(DuelRole.Defender, 5);

        duel.Tick(20f);

        Assert.Equal(130, duel.Defender.Health);
    }

    [Fact]
    public void MinigunNeedsToSpinUp()
    {
        var duel = Fighting(PieceType.Rook, PieceType.Pawn);

        duel.SetTrigger(DuelRole.Attacker, true);
        Assert.False(duel.TryFire(DuelRole.Attacker));
        duel.Tick(0.8f);
        Assert.True(duel.TryFire(DuelRole.Attacker));

        duel.SetTrigger(DuelRole.Attacker, false);
        duel.Tick(1f);
        Assert.False(duel.TryFire(DuelRole.Attacker));
    }

    [Fact]
    public void QueenDashHasACooldown()
    {
        var duel = Fighting(PieceType.Queen, PieceType.Pawn);

        Assert.True(duel.TryDash(DuelRole.Attacker));
        Assert.False(duel.TryDash(DuelRole.Attacker));
        duel.Tick(3f);
        Assert.True(duel.TryDash(DuelRole.Attacker));
        Assert.False(duel.TryDash(DuelRole.Defender));
    }

    [Fact]
    public void QueenHasTwoGrenades()
    {
        var duel = Fighting(PieceType.Queen, PieceType.Pawn);

        Assert.True(duel.TryThrowGrenade(DuelRole.Attacker));
        Assert.True(duel.TryThrowGrenade(DuelRole.Attacker));
        Assert.False(duel.TryThrowGrenade(DuelRole.Attacker));
        Assert.False(duel.TryThrowGrenade(DuelRole.Defender));
    }

    [Theory]
    [InlineData(0f, 60f)]
    [InlineData(2f, 30f)]
    [InlineData(4f, 0f)]
    [InlineData(9f, 0f)]
    public void GrenadeDamageFallsOffLinearly(float distance, float expected)
    {
        var grenades = FighterClasses.For(PieceType.Queen).Grenades!;

        Assert.Equal(expected, grenades.DamageAt(distance), 3);
    }

    [Fact]
    public void EveryPieceHasAClass()
    {
        foreach (var type in Enum.GetValues<PieceType>())
        {
            var fighter = FighterClasses.For(type);
            Assert.Equal(type, fighter.Piece);
            Assert.True(fighter.MaxHealth > 0);
            Assert.True(fighter.Weapon.Magazine > 0);
        }
    }

    [Fact]
    public void ClassesStayWithinTheBalanceBand()
    {
        // Keeps duels winnable for every piece: no class may be a tank or a glass cannon by a wide margin.
        foreach (var type in Enum.GetValues<PieceType>())
        {
            var fighter = FighterClasses.For(type);
            var weapon = fighter.Weapon;
            var damagePerSecond = weapon.Damage * weapon.Pellets / weapon.FireInterval;
            var effectiveHealth = fighter.MaxHealth / fighter.DamageTakenMultiplier;
            Assert.InRange(fighter.MaxHealth, 100, 150);
            Assert.InRange(effectiveHealth, 100, 180);
            Assert.InRange(damagePerSecond, 55, 110);
        }
    }

    [Fact]
    public void AimingHalvesTheSpreadUnlessTheWeaponHasAScope()
    {
        var rifle = FighterClasses.For(PieceType.Pawn).Weapon;
        var sniper = FighterClasses.For(PieceType.Bishop).Weapon;

        Assert.Equal(rifle.SpreadDegrees, rifle.SpreadFor(aiming: false));
        Assert.Equal(rifle.SpreadDegrees / 2f, rifle.SpreadFor(aiming: true));
        Assert.Equal(0f, sniper.SpreadFor(aiming: true));
    }

    [Fact]
    public void ClassPerksMatchTheDesign()
    {
        Assert.True(FighterClasses.For(PieceType.Knight).DoubleJump);
        Assert.NotNull(FighterClasses.For(PieceType.Bishop).ZoomFov);
        Assert.Equal(8, FighterClasses.For(PieceType.Knight).Weapon.Pellets);
        Assert.Equal(0.8f, FighterClasses.For(PieceType.Rook).SpeedMultiplier);
        Assert.NotNull(FighterClasses.For(PieceType.King).Regeneration);
    }
}
