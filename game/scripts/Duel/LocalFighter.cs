using Chess.Core;
using Chess.Core.Duels;
using FpsChess.App;
using FpsChess.Arena;
using Godot;

namespace FpsChess.Duel;

/// <summary>
/// The local player's piece in a duel: first-person movement with the class perks (double jump, dash,
/// slower rook). Shooting is handled by the match screen; this node only moves and aims.
/// </summary>
public partial class LocalFighter : CharacterBody3D
{
    private const float BaseSpeed = 7f;
    private const float SprintMultiplier = 1.45f;
    private const float JumpVelocity = 5.5f;
    private const float MouseSensitivity = 0.0025f;

    private readonly float _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    private Node3D _head = null!;
    private bool _usedDoubleJump;
    private Vector3 _dashVelocity;
    private float _recoil;
    private float _stepDistance;

    /// <summary>Raised every couple of metres walked on the ground.</summary>
    public event Action? Stepped;

    public FighterClass Class { get; private set; } = null!;
    public float PieceHeight { get; private set; }
    public Camera3D Camera { get; private set; } = null!;
    public WeaponView Weapon { get; private set; } = null!;

    /// <summary>False during the intro and countdown: the fighter can look around but not walk.</summary>
    public bool CanMove { get; set; }

    public bool ControlsEnabled => Input.MouseMode == Input.MouseModeEnum.Captured;

    public float Pitch => _head.Rotation.X;

    public void Init(PieceType type, PieceColor color)
    {
        Class = FighterClasses.For(type);
        PieceHeight = PieceModels.Height(type);
        Weapon = new WeaponView();
        Weapon.Init(type, color == PieceColor.White ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.4f, 0.3f));
    }

    public override void _Ready()
    {
        CollisionLayer = BoardGeometry.PlayerLayer;
        CollisionMask = BoardGeometry.WorldLayer | BoardGeometry.PieceLayer;
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = Math.Max(PieceHeight, 1f) },
            Position = new Vector3(0, PieceHeight / 2f, 0),
        });

        _head = new Node3D { Position = new Vector3(0, Ballistics.EyeHeight(PieceHeight), 0) };
        AddChild(_head);
        Camera = new Camera3D { Fov = 80f, Near = 0.03f, Far = 600f };
        _head.AddChild(Camera);
        Camera.AddChild(Weapon);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ControlsEnabled || @event is not InputEventMouseMotion motion) return;
        var zoomFactor = Camera.Fov / 80f;
        RotateY(-motion.Relative.X * MouseSensitivity * zoomFactor);
        var pitch = Mathf.Clamp(_head.Rotation.X - motion.Relative.Y * MouseSensitivity * zoomFactor, -1.45f, 1.45f);
        _head.Rotation = new Vector3(pitch, 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var velocity = Velocity;
        if (IsOnFloor()) _usedDoubleJump = false;
        else velocity.Y -= _gravity * dt;

        var active = ControlsEnabled && CanMove;
        var input = active ? Input.GetVector(InputSetup.Left, InputSetup.Right, InputSetup.Forward, InputSetup.Back) : Vector2.Zero;

        if (active && Input.IsActionJustPressed(InputSetup.Jump))
        {
            if (IsOnFloor()) velocity.Y = JumpVelocity;
            else if (Class.DoubleJump && !_usedDoubleJump)
            {
                velocity.Y = JumpVelocity;
                _usedDoubleJump = true;
            }
        }

        var sprinting = active && Class.Dash is null && Input.IsActionPressed(InputSetup.Sprint);
        var speed = BaseSpeed * Class.SpeedMultiplier * (sprinting ? SprintMultiplier : 1f);
        var direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var blend = Mathf.Min(1f, dt * (IsOnFloor() ? 12f : 3f));
        velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, blend);
        velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * speed, blend);

        Velocity = velocity + _dashVelocity;
        MoveAndSlide();
        Velocity -= _dashVelocity;
        _dashVelocity = _dashVelocity.MoveToward(Vector3.Zero, 120f * dt);

        var horizontal = new Vector2(Velocity.X, Velocity.Z).Length();
        Weapon.SetMovement(IsOnFloor() ? horizontal : 0f);
        if (IsOnFloor() && horizontal > 1f)
        {
            _stepDistance += horizontal * dt;
            if (_stepDistance > 2.2f)
            {
                _stepDistance = 0f;
                Stepped?.Invoke();
            }
        }

        // Camera recoil climbs with each shot and settles back on its own.
        if (_recoil > 0f)
        {
            var settle = Mathf.Min(_recoil, dt * 6f * Mathf.Max(0.02f, _recoil));
            _recoil -= settle;
            _head.Rotation = _head.Rotation with { X = Mathf.Clamp(_head.Rotation.X - settle, -1.45f, 1.45f) };
        }
    }

    /// <summary>Kicks the view up by <paramref name="degrees"/>; it recovers over the next moments.</summary>
    public void Kick(float degrees)
    {
        var radians = Mathf.DegToRad(degrees);
        _recoil += radians;
        _head.Rotation = _head.Rotation with { X = Mathf.Clamp(_head.Rotation.X + radians, -1.45f, 1.45f) };
    }

    /// <summary>Queen perk: a quick burst in the movement direction (or forward when standing still).</summary>
    public void Dash(float distance)
    {
        var input = Input.GetVector(InputSetup.Left, InputSetup.Right, InputSetup.Forward, InputSetup.Back);
        var local = input == Vector2.Zero ? Vector3.Forward : new Vector3(input.X, 0, input.Y);
        // Decelerating from v at 120 m/s^2 covers v^2 / 240 metres.
        _dashVelocity = (Transform.Basis * local).Normalized() * Mathf.Sqrt(distance * 240f);
    }

    public void SetZoom(bool zoomed)
    {
        Camera.Fov = zoomed ? Class.ZoomFov ?? 65f : 80f;
        Weapon.SetAiming(zoomed);
    }

    public Vector3 AimDirection => -Camera.GlobalBasis.Z;
}
