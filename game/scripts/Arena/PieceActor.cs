using Chess.Core;
using Godot;

namespace FpsChess.Fps;

/// <summary>A piece in the 3D world: model plus collider, so the player can bump into it and aim at it.</summary>
public partial class PieceActor : AnimatableBody3D
{
    public const uint PhysicsLayer = 2;

    private Node3D _model = null!;
    private float _hoverOffset;

    public Piece Piece { get; private set; }
    public Square Square { get; set; }

    public static PieceActor Create(Piece piece, Square square)
    {
        var actor = new PieceActor { Square = square, Position = BoardGeometry.Center(square) };
        actor.CollisionLayer = PhysicsLayer;
        actor.CollisionMask = 0;
        actor.SyncToPhysics = false;
        actor.SetPiece(piece);
        return actor;
    }

    /// <summary>Swaps the model, used when a pawn promotes.</summary>
    public void SetPiece(Piece piece)
    {
        Piece = piece;
        foreach (var child in GetChildren()) child.QueueFree();

        _model = PieceModels.Build(piece);
        AddChild(_model);

        var height = PieceModels.Height(piece.Type);
        AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 1.0f * PieceModels.Scale, Height = height },
            Position = new Vector3(0, height / 2f, 0),
        });
    }

    public float Height => PieceModels.Height(Piece.Type);

    /// <summary>World position of the piece's "head", where shots start and land.</summary>
    public Vector3 Head => GlobalPosition + new Vector3(0, Height * 0.75f, 0);

    /// <summary>Selected pieces float a little and spin slowly.</summary>
    public void SetSelected(bool selected)
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(v =>
        {
            _hoverOffset = v;
            _model.Position = new Vector3(0, v, 0);
        }), _hoverOffset, selected ? 0.35f : 0f, 0.2f);
        if (!selected) _model.Rotation = _model.Rotation with { Y = Piece.Color == PieceColor.Black ? Mathf.Pi : 0 };
        SetProcess(selected);
    }

    public override void _Ready() => SetProcess(false);

    public override void _Process(double delta) => _model.RotateY((float)delta * 0.8f);

    public Tween SlideTo(Square target, bool hop, float delay = 0f, float seconds = 0.55f)
    {
        Square = target;
        var to = BoardGeometry.Center(target);
        var from = Position;
        var tween = CreateTween();
        if (delay > 0f) tween.TweenInterval(delay);
        tween.TweenMethod(Callable.From<float>(t =>
        {
            var eased = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            var position = from.Lerp(to, eased);
            // Knights jump over pieces, everything else slides with a small lift.
            position.Y = Mathf.Sin(t * Mathf.Pi) * (hop ? 2.5f : 0.25f);
            Position = position;
        }), 0f, 1f, seconds);
        return tween;
    }

    /// <summary>Hit by a shot: burst into fragments, shrink away and free itself.</summary>
    public void Die()
    {
        CollisionLayer = 0;
        Effects.Burst(GetParent<Node3D>(), Head, PieceModels.Material(Piece.Color).AlbedoColor);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "scale", new Vector3(0.05f, 0.05f, 0.05f), 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_model, "rotation_degrees:y", _model.RotationDegrees.Y + 360f, 0.35f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
