using FpsChess.App;
using Godot;

namespace FpsChess.Fps;

/// <summary>First-person walker: WASD, mouse look, jump and sprint. Input only applies while the mouse is captured.</summary>
public partial class FpsPlayer : CharacterBody3D
{
    private const float WalkSpeed = 7f;
    private const float SprintSpeed = 12f;
    private const float JumpVelocity = 5.5f;
    private const float MouseSensitivity = 0.0025f;
    private const float EyeHeight = 1.7f;

    private readonly float _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    private Node3D _head = null!;

    public Camera3D Camera { get; private set; } = null!;
    public Blaster Blaster { get; private set; } = null!;

    public bool ControlsEnabled => Input.MouseMode == Input.MouseModeEnum.Captured;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | PieceActor.PhysicsLayer;
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
        });

        _head = new Node3D { Position = new Vector3(0, EyeHeight, 0) };
        AddChild(_head);
        Camera = new Camera3D { Fov = 80f, Far = 400f, Current = true };
        _head.AddChild(Camera);
        Blaster = new Blaster();
        Camera.AddChild(Blaster);
    }

    public void TeleportTo(Transform3D spawn)
    {
        GlobalTransform = spawn;
        _head.Rotation = new Vector3(-0.12f, 0, 0);
        Velocity = Vector3.Zero;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ControlsEnabled || @event is not InputEventMouseMotion motion) return;
        RotateY(-motion.Relative.X * MouseSensitivity);
        var pitch = Mathf.Clamp(_head.Rotation.X - motion.Relative.Y * MouseSensitivity, -1.45f, 1.45f);
        _head.Rotation = new Vector3(pitch, 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        if (!IsOnFloor()) velocity.Y -= _gravity * (float)delta;

        var input = ControlsEnabled ? Input.GetVector(InputSetup.Left, InputSetup.Right, InputSetup.Forward, InputSetup.Back) : Vector2.Zero;
        if (ControlsEnabled && Input.IsActionJustPressed(InputSetup.Jump) && IsOnFloor()) velocity.Y = JumpVelocity;

        var speed = ControlsEnabled && Input.IsActionPressed(InputSetup.Sprint) ? SprintSpeed : WalkSpeed;
        var direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var blend = Mathf.Min(1f, (float)delta * (IsOnFloor() ? 12f : 3f));
        velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, blend);
        velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * speed, blend);

        Velocity = velocity;
        MoveAndSlide();
    }

    /// <summary>Where the crosshair ray meets the board plane (y = 0), ignoring pieces in between.</summary>
    public Vector3? AimAtFloor()
    {
        var origin = Camera.GlobalPosition;
        var direction = -Camera.GlobalBasis.Z;
        if (direction.Y > -0.01f) return null;
        return origin + direction * (-origin.Y / direction.Y);
    }

    /// <summary>What the crosshair points at: (collider, hit point), or null.</summary>
    public (GodotObject Collider, Vector3 Point)? Aim(float range = 120f)
    {
        var from = Camera.GlobalPosition;
        var to = from - Camera.GlobalBasis.Z * range;
        var query = PhysicsRayQueryParameters3D.Create(from, to, 1 | PieceActor.PhysicsLayer, [GetRid()]);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;
        return (hit["collider"].AsGodotObject(), hit["position"].AsVector3());
    }
}
