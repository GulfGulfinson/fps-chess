using Chess.Core.Duels;
using FpsChess.Arena;
using FpsChess.Duel;
using Godot;

namespace FpsChess.Net.Server;

/// <summary>
/// The computer's fighter. It walks a navmesh baked from the board, approaches until it can see the
/// opponent, then strafes and shoots. Reaction time and aim error follow the chess AI level.
/// </summary>
public partial class DuelBot : CharacterBody3D
{
    private const float BaseSpeed = 7f;
    private const float PathRefreshSeconds = 0.4f;

    private readonly float _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    private ServerDuel _duel = null!;
    private Fighter3D _self = null!;
    private FighterClass _class = null!;
    private float _reactionSeconds;
    private float _aimErrorDegrees;
    private float _cadence;
    private float _nextShotIn;

    private Vector3[] _path = [];
    private int _pathIndex;
    private float _sincePath = PathRefreshSeconds;
    private float _visibleFor;
    private float _hiddenFor;
    private float _strafeSign = 1f;
    private float _strafeSwitchIn = 1.5f;
    private float _lastHealth;
    private Vector3? _cover;
    private float _inCoverFor;
    private float _coverCooldown;

    public void Init(ServerDuel duel, Fighter3D self, int aiLevel)
    {
        _duel = duel;
        _self = self;
        _class = FighterClasses.For(self.Type);
        // Tuned so level 1 is beatable by a beginner and level 4 still misses a moving target now and then.
        var t = (Math.Clamp(aiLevel, 1, 4) - 1) / 3f;
        _reactionSeconds = Mathf.Lerp(1.0f, 0.35f, t);
        _aimErrorDegrees = Mathf.Lerp(9f, 2.5f, t);
        // Fraction of the weapon's fire rate the bot uses.
        _cadence = Mathf.Lerp(0.55f, 0.87f, t);
        _lastHealth = _class.MaxHealth;
    }

    public void Step(float dt)
    {
        var state = _duel.State;
        var me = state[_self.Role];
        var target = _duel.Opponent(_self);
        var fighting = _duel.IntroDone && state.Phase == DuelPhase.Fighting;

        var velocity = Velocity;
        if (!IsOnFloor()) velocity.Y -= _gravity * dt;

        var toTarget = target.Chest - _self.Eye;
        var flat = new Vector3(toTarget.X, 0, toTarget.Z);
        if (flat.LengthSquared() > 0.01f) Rotation = new Vector3(0, Mathf.Atan2(-flat.X, -flat.Z), 0);

        var desired = Vector3.Zero;
        if (fighting)
        {
            var sees = CanSee(target);
            _visibleFor = sees ? _visibleFor + dt : 0f;
            _hiddenFor = sees ? 0f : _hiddenFor + dt;

            UpdateCover(dt, me, target);
            if (_cover is { } cover)
            {
                desired = FollowPath(cover, dt);
                // Still shoot back if the opponent shows up while we retreat.
                Shoot(sees && !me.IsReloading, toTarget);
            }
            else
            {
                desired = sees ? Strafe(flat, dt) : FollowPath(target.Position, dt);
                Shoot(sees, toTarget);
                UsePerks(sees, target, flat, me);
                // An occasional hop while strafing makes the bot harder to hit.
                if (sees && IsOnFloor() && GD.Randf() < 0.006f) velocity.Y = 5.5f;
            }
        }

        var speed = BaseSpeed * _class.SpeedMultiplier;
        velocity.X = Mathf.Lerp(velocity.X, desired.X * speed, Mathf.Min(1f, dt * 10f));
        velocity.Z = Mathf.Lerp(velocity.Z, desired.Z * speed, Mathf.Min(1f, dt * 10f));
        Velocity = velocity;
        MoveAndSlide();
        _lastHealth = me.Health;
    }

    /// <summary>Retreat behind cover to reload or recover, then come back out.</summary>
    private void UpdateCover(float dt, Chess.Core.Duels.Fighter me, Fighter3D target)
    {
        _coverCooldown = Mathf.Max(0f, _coverCooldown - dt);
        var hurt = me.Health < me.Class.MaxHealth * 0.35f;
        var wantsCover = me.IsReloading || (hurt && me.Class.Regeneration is not null);

        if (_cover is null)
        {
            if ((wantsCover || hurt) && _coverCooldown <= 0f) _cover = FindCover(target);
            _inCoverFor = 0f;
            return;
        }

        if (GlobalPosition.DistanceTo(_cover.Value) < 1.2f) _inCoverFor += dt;
        // Leave cover once the reason is gone, or after a while so the bot never just hides out the clock.
        var regenerating = me.Class.Regeneration is not null && me.Health < me.Class.MaxHealth * 0.7f;
        if ((!me.IsReloading && !regenerating && _inCoverFor > 0.8f) || _inCoverFor > 4f)
        {
            _cover = null;
            _coverCooldown = 5f;
        }
    }

    /// <summary>The nearest reachable spot within a few metres that the opponent cannot see.</summary>
    private Vector3? FindCover(Fighter3D target)
    {
        var space = GetWorld3D().DirectSpaceState;
        Vector3? best = null;
        var bestDistance = float.MaxValue;
        for (var ring = 3f; ring <= 9f; ring += 3f)
        {
            for (var i = 0; i < 16; i++)
            {
                var angle = Mathf.Tau / 16f * i;
                var candidate = GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring;
                var onMesh = NavigationServer3D.MapGetClosestPoint(_duel.NavigationMap, candidate);
                if (onMesh.DistanceTo(candidate) > 0.6f || Mathf.Abs(onMesh.Y - GlobalPosition.Y) > 0.5f) continue;

                var chest = onMesh + Vector3.Up * Ballistics.ChestHeight(_self.Height);
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(target.Eye, chest,
                    BoardGeometry.WorldLayer | BoardGeometry.PieceLayer));
                if (hit.Count == 0) continue;
                var distance = GlobalPosition.DistanceTo(onMesh);
                if (distance < bestDistance)
                {
                    best = onMesh;
                    bestDistance = distance;
                }
            }
            if (best is not null) break;
        }
        return best;
    }

    private bool CanSee(Fighter3D target)
    {
        var query = PhysicsRayQueryParameters3D.Create(_self.Eye, target.Chest,
            BoardGeometry.WorldLayer | BoardGeometry.PieceLayer | BoardGeometry.HitboxLayer, [GetRid()]);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target.Body;
    }

    private Vector3 Strafe(Vector3 flatToTarget, float dt)
    {
        _strafeSwitchIn -= dt;
        if (_strafeSwitchIn <= 0f)
        {
            _strafeSign = -_strafeSign;
            _strafeSwitchIn = (float)GD.RandRange(1.0, 2.2);
        }

        var forward = flatToTarget.Normalized();
        var side = forward.Cross(Vector3.Up) * _strafeSign;
        var distance = flatToTarget.Length();
        var preferred = _class.Weapon.Pellets > 1 ? 6f : _class.ZoomFov is not null ? 22f : 12f;
        var approach = distance > preferred + 3f ? 0.7f : distance < preferred - 3f ? -0.7f : 0f;
        var move = (side + forward * approach).Normalized();

        // Never strafe off the table or into a wall: only step where the navmesh continues.
        if (!IsWalkable(GlobalPosition + move * 1.5f))
        {
            _strafeSign = -_strafeSign;
            move = (forward.Cross(Vector3.Up) * _strafeSign + forward * approach).Normalized();
            if (!IsWalkable(GlobalPosition + move * 1.5f)) move = Vector3.Zero;
        }
        return move;
    }

    private bool IsWalkable(Vector3 point)
    {
        var closest = NavigationServer3D.MapGetClosestPoint(_duel.NavigationMap, point);
        return closest.DistanceTo(point) < 0.6f;
    }

    private Vector3 FollowPath(Vector3 goal, float dt)
    {
        _sincePath += dt;
        if (_sincePath >= PathRefreshSeconds)
        {
            _sincePath = 0f;
            _path = NavigationServer3D.MapGetPath(_duel.NavigationMap, GlobalPosition, goal, true);
            _pathIndex = 0;
        }

        while (_pathIndex < _path.Length)
        {
            var to = _path[_pathIndex] - GlobalPosition;
            to.Y = 0;
            if (to.Length() > 0.5f) return to.Normalized();
            _pathIndex++;
        }
        return Vector3.Zero;
    }

    private void Shoot(bool sees, Vector3 toTarget)
    {
        var state = _duel.State;
        var needsSpinUp = _class.Weapon.SpinUpSeconds > 0f;
        if (needsSpinUp) state.SetTrigger(_self.Role, sees);
        _nextShotIn -= (float)GetPhysicsProcessDeltaTime();
        if (!sees || _visibleFor < _reactionSeconds || _nextShotIn > 0f) return;

        var directions = Ballistics.ShotDirections(toTarget, _class.Weapon, aiming: _class.ZoomFov is not null, _aimErrorDegrees);
        if (_duel.Fire(_self, _self.Eye, directions)) _nextShotIn = _class.Weapon.FireInterval / _cadence;
    }

    private void UsePerks(bool sees, Fighter3D target, Vector3 flatToTarget, Chess.Core.Duels.Fighter me)
    {
        // Dodge when hit.
        if (_class.Dash is { } dash && me.Health < _lastHealth && _duel.State.TryDash(_self.Role))
        {
            var side = flatToTarget.Normalized().Cross(Vector3.Up) * _strafeSign;
            if (IsWalkable(GlobalPosition + side * dash.Distance)) Velocity += side * dash.Distance * 4f;
        }

        // Lob a grenade at an opponent hiding nearby.
        if (_class.Grenades is not null && !sees && _hiddenFor > 2.5f && flatToTarget.Length() < 18f)
        {
            var lob = flatToTarget.Normalized() * Mathf.Min(14f, flatToTarget.Length() * 0.9f) + Vector3.Up * 6f;
            if (_duel.ThrowGrenade(_self, _self.Eye + flatToTarget.Normalized() * 0.8f, lob)) _hiddenFor = 0f;
        }
    }
}
