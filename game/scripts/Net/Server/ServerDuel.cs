using Chess.Core;
using Chess.Core.Duels;
using FpsChess.Arena;
using FpsChess.Duel;
using Godot;

namespace FpsChess.Net.Server;

/// <summary>
/// The authoritative side of one duel. Players report their pose and shots; the server checks the shot
/// origin, asks <see cref="DuelState"/> whether the weapon may fire, raycasts in the room's own world and
/// applies the damage. A computer-controlled side is a <see cref="DuelBot"/> living in the same world.
/// </summary>
public partial class ServerDuel : Node
{
    // Shots arrive with network jitter; forgive this much remaining cooldown.
    private const float FireTolerance = 0.05f;
    private const float OutcomeDisplaySeconds = 1.2f;

    private readonly List<(RigidBody3D Body, DuelRole Thrower, float FuseLeft)> _grenades = [];
    private readonly Dictionary<DuelRole, PieceColor> _colors = [];
    private PieceColor _attackerColor;
    private MatchRoom _room = null!;
    private Node3D _world = null!;
    private Node3D _content = null!;
    private Fighter3D _attacker = null!;
    private Fighter3D _defender = null!;
    private float _introLeft = NetConfig.DuelIntroSeconds;
    private float _time;
    private float _sinceSnapshot;
    private float _outcomeShownFor;
    private bool _finished;

    public DuelState State { get; private set; } = null!;
    public Rid NavigationMap => _world.GetWorld3D().NavigationMap;

    public void Init(MatchRoom room, Node3D world, PendingDuel pending, Position position, int aiLevel, int attackerPeer, int defenderPeer)
    {
        Name = "Duel";
        _room = room;
        _world = world;
        State = new DuelState(pending.Attacker.Type, pending.Defender.Type);
        _attackerColor = pending.Attacker.Color;

        _content = new Node3D { Name = "DuelContent" };
        world.AddChild(_content);

        // Every other piece is static cover.
        foreach (var (square, piece) in position.Pieces())
        {
            if (square == pending.AttackerSquare || square == pending.DefenderSquare) continue;
            var body = new StaticBody3D { Position = BoardGeometry.Center(square), CollisionLayer = BoardGeometry.PieceLayer, CollisionMask = 0 };
            var height = PieceModels.Height(piece.Type);
            body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = PieceModels.ColliderRadius, Height = height }, Position = new Vector3(0, height / 2f, 0) });
            _content.AddChild(body);
        }

        var attackerAt = BoardGeometry.Center(pending.AttackerSquare);
        var defenderAt = BoardGeometry.Center(pending.DefenderSquare);
        _attacker = CreateFighter(DuelRole.Attacker, pending.Attacker.Type, attackerPeer, attackerAt, defenderAt, aiLevel);
        _defender = CreateFighter(DuelRole.Defender, pending.Defender.Type, defenderPeer, defenderAt, attackerAt, aiLevel);

        if (_attacker.Bot is not null || _defender.Bot is not null) BakeNavigation();
    }

    private Fighter3D CreateFighter(DuelRole role, PieceType type, int peer, Vector3 at, Vector3 facing, int aiLevel)
    {
        var height = PieceModels.Height(type);
        var shape = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = Math.Max(height, 1f) },
            Position = new Vector3(0, height / 2f, 0),
        };
        var yaw = BoardGeometry.YawTowards(at, facing);

        CollisionObject3D body;
        DuelBot? bot = null;
        if (peer == 0)
        {
            bot = new DuelBot();
            bot.AddChild(shape);
            body = bot;
        }
        else
        {
            body = new StaticBody3D();
            body.AddChild(shape);
        }
        body.CollisionLayer = BoardGeometry.HitboxLayer;
        body.CollisionMask = BoardGeometry.WorldLayer | BoardGeometry.PieceLayer;
        body.Position = at;
        body.Rotation = new Vector3(0, yaw, 0);
        _content.AddChild(body);

        var fighter = new Fighter3D(role, type, peer, body, bot, height) { Position = at, Yaw = yaw };
        _colors[role] = role == DuelRole.Attacker ? _attackerColor : _attackerColor.Opponent();
        bot?.Init(this, fighter, aiLevel);
        return fighter;
    }

    private void BakeNavigation()
    {
        var mesh = new NavigationMesh
        {
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
            GeometryCollisionMask = BoardGeometry.WorldLayer | BoardGeometry.PieceLayer,
            AgentRadius = 0.6f,
            AgentHeight = 1.6f,
            AgentMaxClimb = 0.4f,
            CellSize = 0.3f,
            CellHeight = 0.2f,
        };
        var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(mesh, source, _world);
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);
        _content.AddChild(new NavigationRegion3D { NavigationMesh = mesh });
    }

    public override void _ExitTree() => _content.QueueFree();

    public Fighter3D this[DuelRole role] => role == DuelRole.Attacker ? _attacker : _defender;

    public Fighter3D Opponent(Fighter3D fighter) => fighter == _attacker ? _defender : _attacker;

    /// <summary>A player came back with a new connection and takes over their fighter.</summary>
    public void ReassignPeer(PieceColor color, int peer)
    {
        foreach (var fighter in new[] { _attacker, _defender })
        {
            if (_colors[fighter.Role] == color && fighter.Bot is null) fighter.Peer = peer;
        }
    }

    private Fighter3D? ByPeer(int peer) => _attacker.Peer == peer ? _attacker : _defender.Peer == peer ? _defender : null;

    /// <summary>True once the intro is over; bots and players may act from then on (the state enforces the countdown).</summary>
    public bool IntroDone => _introLeft <= 0f;

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        try
        {
            Step(dt);
        }
        catch (Exception e)
        {
            _room.Abort(e);
        }
    }

    private void Step(float dt)
    {
        _time += dt;
        if (_introLeft > 0f) _introLeft -= dt;
        else State.Tick(dt);

        foreach (var fighter in new[] { _attacker, _defender })
        {
            fighter.Bot?.Step(dt);
            if (fighter.Bot is not null) fighter.SyncFromBody();
            fighter.History.Record(_time, fighter.Position.ToNumerics());
            if (fighter.Position.Y < BoardGeometry.FallHeight) State.Fall(fighter.Role);
        }

        TickGrenades(dt);

        _sinceSnapshot += dt;
        if (_sinceSnapshot >= NetConfig.SyncInterval)
        {
            _sinceSnapshot = 0f;
            SendSnapshot();
        }

        if (State.Outcome is { } outcome && !_finished)
        {
            _outcomeShownFor += dt;
            if (_outcomeShownFor >= OutcomeDisplaySeconds)
            {
                _finished = true;
                SendSnapshot();
                _room.FinishDuel(outcome);
            }
        }
    }

    private void SendSnapshot()
    {
        var stats = new float[SnapshotField.Count];
        stats[SnapshotField.AttackerHealth] = State.Attacker.Health;
        stats[SnapshotField.DefenderHealth] = State.Defender.Health;
        stats[SnapshotField.AttackerAmmo] = State.Attacker.Ammo;
        stats[SnapshotField.DefenderAmmo] = State.Defender.Ammo;
        stats[SnapshotField.AttackerReload] = State.Attacker.ReloadRemaining;
        stats[SnapshotField.DefenderReload] = State.Defender.ReloadRemaining;
        stats[SnapshotField.TimeLeft] = State.TimeLeft;
        stats[SnapshotField.CountdownLeft] = State.CountdownLeft;
        stats[SnapshotField.Phase] = (int)State.Phase;
        stats[SnapshotField.AttackerGrenades] = State.Attacker.GrenadesLeft;
        stats[SnapshotField.DefenderGrenades] = State.Defender.GrenadesLeft;
        stats[SnapshotField.AttackerDashCooldown] = State.Attacker.DashCooldown;
        stats[SnapshotField.DefenderDashCooldown] = State.Defender.DashCooldown;
        stats[SnapshotField.AttackerSpinUp] = State.Attacker.SpinUp;
        stats[SnapshotField.DefenderSpinUp] = State.Defender.SpinUp;
        stats[SnapshotField.IntroLeft] = Math.Max(0f, _introLeft);
        _room.SendAll(MatchChannel.MethodName.DuelSnapshot,
            _attacker.Position, _attacker.Yaw, _attacker.Pitch, _defender.Position, _defender.Yaw, _defender.Pitch, stats);
    }

    // ---------------------------------------------------------------- player input

    public void OnInput(int peer, Vector3 position, float yaw, float pitch)
    {
        if (ByPeer(peer) is not { } fighter || State.Phase == DuelPhase.Over) return;
        // Before the duel starts players stay on their square.
        if (!IntroDone || State.Phase == DuelPhase.Countdown) position = fighter.Body.GlobalPosition;
        fighter.Position = position;
        fighter.Yaw = yaw;
        fighter.Pitch = pitch;
        fighter.Body.GlobalPosition = position;
    }

    public void OnTrigger(int peer, bool held)
    {
        if (ByPeer(peer) is { } fighter) State.SetTrigger(fighter.Role, held);
    }

    public void OnFire(int peer, Vector3 origin, Vector3[] ends, int hitMask)
    {
        if (ByPeer(peer) is not { } fighter) return;
        if (origin.DistanceTo(fighter.Eye) > NetConfig.MaxShotOriginError)
        {
            GD.PushWarning($"[duel] rejected shot from {fighter.Role}: origin {origin} is {origin.DistanceTo(fighter.Eye):0.00} m from the known eye {fighter.Eye}");
            return;
        }
        FireAsSeen(fighter, origin, ends, hitMask);
    }

    /// <summary>
    /// A player's shot: the client reports where its pellets ended and which ones hit the opponent as it
    /// was drawn on screen. A claimed hit counts if the opponent really stood there within the last moments
    /// and nothing solid lies between the muzzle and the hit point.
    /// </summary>
    private void FireAsSeen(Fighter3D shooter, Vector3 origin, Vector3[] ends, int hitMask)
    {
        var weapon = FighterClasses.For(shooter.Type).Weapon;
        if (ends.Length == 0 || ends.Length > weapon.Pellets) return;
        if (!State.TryFire(shooter.Role, FireTolerance)) return;

        var target = Opponent(shooter);
        var space = _world.GetWorld3D().DirectSpaceState;
        var hits = 0;
        for (var i = 0; i < ends.Length; i++)
        {
            if ((hitMask & (1 << i)) == 0) continue;
            var end = ends[i];
            if (origin.DistanceTo(end) > weapon.MaxRange + 1f) continue;
            if (!target.History.Contains(end.ToNumerics(), _time, 0.45f + NetConfig.HitTolerance, target.Height)) continue;

            // Walls and pieces still block: stop just short of the hit point so the target itself is not tested.
            var direction = (end - origin).Normalized();
            var clear = end - direction * 0.3f;
            var blocked = space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, clear,
                BoardGeometry.WorldLayer | BoardGeometry.PieceLayer, [shooter.Body.GetRid()]));
            if (blocked.Count == 0) hits++;
        }

        _room.SendAll(MatchChannel.MethodName.ShotFired, (int)shooter.Role, origin, ends);
        if (hits > 0) Damage(target.Role, weapon.Damage * hits);
    }

    public void OnReload(int peer)
    {
        if (ByPeer(peer) is { } fighter) State.StartReload(fighter.Role);
    }

    public void OnDash(int peer)
    {
        if (ByPeer(peer) is { } fighter) State.TryDash(fighter.Role);
    }

    public void OnThrowGrenade(int peer, Vector3 origin, Vector3 velocity)
    {
        if (ByPeer(peer) is not { } fighter || origin.DistanceTo(fighter.Eye) > NetConfig.MaxShotOriginError) return;
        ThrowGrenade(fighter, origin, velocity.LimitLength(25f));
    }

    // ---------------------------------------------------------------- shared by players and bots

    /// <summary>A shot resolved entirely on the server (the bot), raycasting against the current positions.</summary>
    /// <returns>Whether the weapon fired.</returns>
    public bool Fire(Fighter3D shooter, Vector3 origin, Vector3[] directions)
    {
        var weapon = FighterClasses.For(shooter.Type).Weapon;
        if (directions.Length == 0 || directions.Length > weapon.Pellets) return false;
        if (!State.TryFire(shooter.Role, FireTolerance)) return false;

        var target = Opponent(shooter);
        var space = _world.GetWorld3D().DirectSpaceState;
        var ends = new Vector3[directions.Length];
        var hits = 0;
        for (var i = 0; i < directions.Length; i++)
        {
            var direction = directions[i].Normalized();
            var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * weapon.MaxRange,
                BoardGeometry.WorldLayer | BoardGeometry.PieceLayer | BoardGeometry.HitboxLayer, [shooter.Body.GetRid()]);
            var hit = space.IntersectRay(query);
            if (hit.Count == 0)
            {
                ends[i] = origin + direction * weapon.MaxRange;
                continue;
            }
            ends[i] = hit["position"].AsVector3();
            if (hit["collider"].AsGodotObject() == target.Body) hits++;
        }

        _room.SendAll(MatchChannel.MethodName.ShotFired, (int)shooter.Role, origin, ends);
        if (hits > 0) Damage(target.Role, weapon.Damage * hits);
        return true;
    }

    public bool ThrowGrenade(Fighter3D thrower, Vector3 origin, Vector3 velocity)
    {
        if (FighterClasses.For(thrower.Type).Grenades is not { } grenades || !State.TryThrowGrenade(thrower.Role)) return false;

        var body = new RigidBody3D
        {
            Mass = 0.5f,
            CollisionLayer = 0,
            CollisionMask = BoardGeometry.WorldLayer | BoardGeometry.PieceLayer | BoardGeometry.HitboxLayer,
            LinearVelocity = velocity,
        };
        body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.25f } });
        _content.AddChild(body);
        body.GlobalPosition = origin;
        body.AddCollisionExceptionWith(thrower.Body);
        _grenades.Add((body, thrower.Role, grenades.FuseSeconds));
        _room.SendAll(MatchChannel.MethodName.GrenadeThrown, (int)thrower.Role, origin, velocity);
        return true;
    }

    private void TickGrenades(float dt)
    {
        for (var i = _grenades.Count - 1; i >= 0; i--)
        {
            var (body, thrower, fuse) = _grenades[i];
            fuse -= dt;
            if (fuse > 0f)
            {
                _grenades[i] = (body, thrower, fuse);
                continue;
            }

            _grenades.RemoveAt(i);
            var at = body.GlobalPosition;
            body.QueueFree();
            _room.SendAll(MatchChannel.MethodName.GrenadeExploded, at);

            var perk = FighterClasses.For(this[thrower].Type).Grenades!;
            foreach (var fighter in new[] { _attacker, _defender })
            {
                var damage = perk.DamageAt(at.DistanceTo(fighter.Chest));
                if (damage > 0f) Damage(fighter.Role, damage);
            }
        }
    }

    private void Damage(DuelRole target, float amount)
    {
        var applied = State.ApplyDamage(target, amount);
        if (applied > 0f) _room.SendAll(MatchChannel.MethodName.Damaged, (int)target, applied);
    }
}

/// <summary>Server-side view of one fighter: who controls it, its body in the room world and its last pose.</summary>
public sealed class Fighter3D(DuelRole role, PieceType type, int peer, CollisionObject3D body, DuelBot? bot, float height)
{
    public DuelRole Role { get; } = role;
    public PieceType Type { get; } = type;

    /// <summary>0 for the computer; changes when a player reconnects.</summary>
    public int Peer { get; set; } = peer;

    public CollisionObject3D Body { get; } = body;
    public DuelBot? Bot { get; } = bot;
    public float Height { get; } = height;
    public Vector3 Position { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    /// <summary>Recent positions, for checking hits the shooter saw with a delay.</summary>
    public PoseHistory History { get; } = new(NetConfig.HitHistorySeconds);

    public Vector3 Eye => Position + Vector3.Up * Ballistics.EyeHeight(Height);
    public Vector3 Chest => Position + Vector3.Up * Ballistics.ChestHeight(Height);

    public void SyncFromBody()
    {
        Position = Body.GlobalPosition;
        Yaw = Body.Rotation.Y;
    }
}
