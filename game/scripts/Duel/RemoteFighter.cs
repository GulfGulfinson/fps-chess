using Chess.Core;
using FpsChess.Arena;
using Godot;

namespace FpsChess.Duel;

/// <summary>The opponent's piece during a duel, smoothly following the poses the server sends.</summary>
public partial class RemoteFighter : AnimatableBody3D
{
    private Vector3 _targetPosition;
    private Label3D _marker = null!;
    private Label3D _status = null!;
    private string _ownerName = "";
    private float _targetYaw;
    private Piece _piece;

    public void Init(Piece piece, Vector3 position, float yaw, string ownerName)
    {
        _piece = piece;
        _ownerName = ownerName;
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
        model.MaterialOverlay = PieceModels.EnemyOutline;
        AddChild(model);

        // Marker and health above the head, drawn through cover so the opponent can always be found.
        _marker = Marker("\u25BC", 72, new Vector3(0, PieceHeight + 0.7f, 0));
        _status = Marker("", 36, new Vector3(0, PieceHeight + 1.25f, 0));
        AddChild(_marker);
        AddChild(_status);
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = Math.Max(PieceHeight, 1f) },
            Position = new Vector3(0, PieceHeight / 2f, 0),
        });
    }

    private static Label3D Marker(string text, int size, Vector3 position) => new()
    {
        Text = text,
        Position = position,
        FontSize = size,
        PixelSize = 0.0011f,
        FixedSize = true,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
        Modulate = new Color(1f, 0.3f, 0.2f),
        OutlineModulate = Colors.Black,
        OutlineSize = 10,
        Font = Ui.UiTheme.Bold,
    };

    public void SetHealth(float health, float max) =>
        _status.Text = $"{_ownerName}: {Ui.Texts.Piece(_piece.Type)}  {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(max)}";

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
