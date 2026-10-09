using Chess.Core;
using FpsChess.Arena;
using Godot;

namespace FpsChess.Duel;

/// <summary>The opponent's piece during a duel, smoothly following the poses the server sends.</summary>
public partial class RemoteFighter : AnimatableBody3D
{
    private Vector3 _targetPosition;
    private float _targetYaw;
    private Piece _piece;

    public void Init(Piece piece, Vector3 position, float yaw)
    {
        _piece = piece;
        Position = position;
        _targetPosition = position;
        _targetYaw = yaw;
        Rotation = new Vector3(0, yaw, 0);
    }

    public float PieceHeight => PieceModels.Height(_piece.Type);

    public override void _Ready()
    {
        SyncToPhysics = false;
        CollisionLayer = BoardGeometry.PieceLayer;
        CollisionMask = 0;
        var model = PieceModels.Build(_piece);
        // Models face -Z already; the body's yaw turns them. Undo the built-in turn for black.
        model.Rotation = Vector3.Zero;
        AddChild(model);
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = Math.Max(PieceHeight, 1f) },
            Position = new Vector3(0, PieceHeight / 2f, 0),
        });
    }

    public void SetPose(Vector3 position, float yaw)
    {
        _targetPosition = position;
        _targetYaw = yaw;
    }

    public override void _Process(double delta)
    {
        var t = Mathf.Min(1f, (float)delta * 15f);
        Position = Position.Lerp(_targetPosition, t);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _targetYaw, t), 0);
    }

    public Vector3 Head => GlobalPosition + Vector3.Up * PieceHeight * 0.85f;
}
