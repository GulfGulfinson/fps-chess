namespace Chess.Core.Duels;

public enum DuelRole
{
    Attacker,
    Defender,
}

public enum DuelPhase
{
    Countdown,
    Fighting,
    Over,
}

/// <summary>Live state of one fighter. Mutated only through <see cref="DuelState"/>.</summary>
public sealed class Fighter
{
    internal Fighter(FighterClass fighterClass)
    {
        Class = fighterClass;
        Health = fighterClass.MaxHealth;
        Ammo = fighterClass.Weapon.Magazine;
        GrenadesLeft = fighterClass.Grenades?.Count ?? 0;
    }

    public FighterClass Class { get; }
    public float Health { get; internal set; }
    public int Ammo { get; internal set; }
    public bool IsReloading => ReloadRemaining > 0f;
    public float ReloadRemaining { get; internal set; }
    public float FireCooldown { get; internal set; }
    public float SpinUp { get; internal set; }
    public bool TriggerHeld { get; internal set; }
    public float DashCooldown { get; internal set; }
    public int GrenadesLeft { get; internal set; }
    public float SinceDamage { get; internal set; }
    public bool IsAlive => Health > 0f;
}

/// <summary>
/// The rules of one duel, independent of rendering and physics: health, damage, ammunition, cooldowns,
/// perks, the countdown and the time limit. Like <see cref="ChessClock"/> it advances only through
/// <see cref="Tick"/>, so it is deterministic and unit-testable. Movement and hit detection happen in the game.
/// </summary>
public sealed class DuelState
{
    public const float CountdownSeconds = 3f;
    public const float TimeLimitSeconds = 60f;

    // Float timers are compared with a small tolerance so exact multiples of an interval are accepted.
    private const float Epsilon = 1e-4f;

    public DuelState(PieceType attacker, PieceType defender)
    {
        Attacker = new Fighter(FighterClasses.For(attacker));
        Defender = new Fighter(FighterClasses.For(defender));
    }

    public Fighter Attacker { get; }
    public Fighter Defender { get; }
    public Fighter this[DuelRole role] => role == DuelRole.Attacker ? Attacker : Defender;

    public DuelPhase Phase { get; private set; } = DuelPhase.Countdown;
    public float CountdownLeft { get; private set; } = CountdownSeconds;
    public float TimeLeft { get; private set; } = TimeLimitSeconds;
    public DuelOutcome? Outcome { get; private set; }

    public void Tick(float seconds)
    {
        if (Phase == DuelPhase.Over || seconds <= 0f) return;

        if (Phase == DuelPhase.Countdown)
        {
            CountdownLeft -= seconds;
            if (CountdownLeft > Epsilon) return;
            seconds = -CountdownLeft;
            CountdownLeft = 0f;
            Phase = DuelPhase.Fighting;
        }

        TickFighter(Attacker, seconds);
        TickFighter(Defender, seconds);

        TimeLeft -= seconds;
        if (TimeLeft <= Epsilon)
        {
            TimeLeft = 0f;
            End(DuelOutcome.DefenderWon);
        }
    }

    private static void TickFighter(Fighter fighter, float seconds)
    {
        fighter.FireCooldown = Math.Max(0f, fighter.FireCooldown - seconds);
        fighter.DashCooldown = Math.Max(0f, fighter.DashCooldown - seconds);

        if (fighter.IsReloading)
        {
            fighter.ReloadRemaining -= seconds;
            if (fighter.ReloadRemaining <= Epsilon)
            {
                fighter.ReloadRemaining = 0f;
                fighter.Ammo = fighter.Class.Weapon.Magazine;
            }
        }

        var spinUp = fighter.Class.Weapon.SpinUpSeconds;
        if (spinUp > 0f)
        {
            // Spinning down is twice as fast as spinning up.
            fighter.SpinUp = fighter.TriggerHeld
                ? Math.Min(spinUp, fighter.SpinUp + seconds)
                : Math.Max(0f, fighter.SpinUp - seconds * 2f);
        }

        var before = fighter.SinceDamage;
        fighter.SinceDamage += seconds;
        if (fighter.Class.Regeneration is { } regeneration && fighter.IsAlive)
        {
            var regenSeconds = Math.Max(0f, fighter.SinceDamage - regeneration.DelaySeconds) -
                               Math.Max(0f, before - regeneration.DelaySeconds);
            fighter.Health = Math.Min(fighter.Class.MaxHealth, fighter.Health + regenSeconds * regeneration.HealthPerSecond);
        }
    }

    public void SetTrigger(DuelRole role, bool held) => this[role].TriggerHeld = held;

    /// <summary>
    /// Fires one shot if the weapon is ready. An empty magazine starts a reload instead.
    /// The caller resolves the hit (raycasts) and reports damage through <see cref="ApplyDamage"/>.
    /// </summary>
    /// <param name="tolerance">
    /// Seconds of remaining cooldown to forgive. The server uses this because a client's shots arrive with
    /// network jitter and would otherwise be rejected just before the cooldown runs out.
    /// </param>
    public bool TryFire(DuelRole role, float tolerance = 0f)
    {
        var fighter = this[role];
        if (Phase != DuelPhase.Fighting || fighter.IsReloading || fighter.FireCooldown > tolerance + Epsilon) return false;
        if (fighter.Ammo == 0)
        {
            StartReload(role);
            return false;
        }
        if (fighter.SpinUp + Epsilon < fighter.Class.Weapon.SpinUpSeconds) return false;

        fighter.Ammo--;
        fighter.FireCooldown = fighter.Class.Weapon.FireInterval;
        return true;
    }

    public bool StartReload(DuelRole role)
    {
        var fighter = this[role];
        if (Phase == DuelPhase.Over || fighter.IsReloading || fighter.Ammo == fighter.Class.Weapon.Magazine) return false;
        fighter.ReloadRemaining = fighter.Class.Weapon.ReloadSeconds;
        return true;
    }

    /// <returns>The damage actually applied after armour.</returns>
    public float ApplyDamage(DuelRole target, float amount)
    {
        var fighter = this[target];
        if (Phase != DuelPhase.Fighting || amount <= 0f) return 0f;

        var applied = Math.Min(fighter.Health, amount * fighter.Class.DamageTakenMultiplier);
        fighter.Health -= applied;
        fighter.SinceDamage = 0f;
        if (!fighter.IsAlive) End(target == DuelRole.Attacker ? DuelOutcome.DefenderWon : DuelOutcome.AttackerWon);
        return applied;
    }

    public bool TryDash(DuelRole role)
    {
        var fighter = this[role];
        if (Phase != DuelPhase.Fighting || fighter.Class.Dash is not { } dash || fighter.DashCooldown > Epsilon) return false;
        fighter.DashCooldown = dash.Cooldown;
        return true;
    }

    public bool TryThrowGrenade(DuelRole role)
    {
        var fighter = this[role];
        if (Phase != DuelPhase.Fighting || fighter.GrenadesLeft == 0) return false;
        fighter.GrenadesLeft--;
        return true;
    }

    /// <summary>The fighter dropped off the table.</summary>
    public void Fall(DuelRole role)
    {
        if (Phase == DuelPhase.Over) return;
        this[role].Health = 0f;
        End(role == DuelRole.Attacker ? DuelOutcome.DefenderWon : DuelOutcome.AttackerWon);
    }

    private void End(DuelOutcome outcome)
    {
        Outcome = outcome;
        Phase = DuelPhase.Over;
        Attacker.TriggerHeld = false;
        Defender.TriggerHeld = false;
    }
}
