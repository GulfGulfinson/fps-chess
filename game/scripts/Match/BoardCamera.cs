using Chess.Core;
using Godot;

namespace FpsChess.Match;

/// <summary>Looks down at the board from the player's side. Right mouse drag orbits, the wheel zooms.</summary>
public partial class BoardCamera : Camera3D
{
    private const float MinPitch = 0.45f;
    private const float MaxPitch = 1.4f;
    private const float MinDistance = 22f;
    private const float MaxDistance = 75f;

    private float _yaw;
    private float _pitch = Mathf.DegToRad(55f);
    private float _distance = 44f;
    private bool _dragging;

    /// <summary>While false (camera flights into a duel), input and orbit placement are ignored.</summary>
    public bool Orbiting { get; set; } = true;

    public void FaceFrom(PieceColor side) => _yaw = side == PieceColor.White ? 0f : Mathf.Pi;

    public override void _Ready()
    {
        Fov = 60f;
        Far = 800f;
        GlobalTransform = OrbitTransform;
    }

    /// <summary>Where the camera sits for the current orbit settings.</summary>
    public Transform3D OrbitTransform
    {
        get
        {
            var offset = new Vector3(Mathf.Sin(_yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _distance;
            // Aim slightly in front of the centre so the near ranks are not cut off.
            var target = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)) * 2f;
            return new Transform3D(Basis.Identity, target + offset).LookingAt(target, Vector3.Up);
        }
    }

    /// <returns>True when the event was used for the camera.</returns>
    public bool HandleInput(InputEvent @event)
    {
        if (!Orbiting) return false;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
                _dragging = button.Pressed;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _distance = Mathf.Max(MinDistance, _distance - 3f);
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _distance = Mathf.Min(MaxDistance, _distance + 3f);
                return true;
            case InputEventMouseMotion motion when _dragging:
                _yaw -= motion.Relative.X * 0.006f;
                _pitch = Mathf.Clamp(_pitch + motion.Relative.Y * 0.004f, MinPitch, MaxPitch);
                return true;
        }
        return false;
    }

    public override void _Process(double delta)
    {
        if (!Orbiting) return;
        GlobalTransform = GlobalTransform.InterpolateWith(OrbitTransform, Mathf.Min(1f, (float)delta * 12f));
    }
}
