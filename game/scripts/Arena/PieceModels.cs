using Chess.Core;
using Godot;

namespace FpsChess.Arena;

/// <summary>
/// Builds the giant 3D pieces from primitive meshes, so the project needs no model files.
/// Models stand on y = 0 and face -Z (towards black); black pieces are turned around by the caller.
/// </summary>
public static class PieceModels
{
    private static StandardMaterial3D? _white;
    private static StandardMaterial3D? _black;

    public static StandardMaterial3D Material(PieceColor color) => color == PieceColor.White
        ? _white ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.93f, 0.9f, 0.82f),
            Roughness = 0.35f,
            RimEnabled = true,
            Rim = 0.25f,
        }
        : _black ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.08f, 0.1f),
            Roughness = 0.22f,
            Metallic = 0.35f,
            RimEnabled = true,
            Rim = 0.4f,
        };

    /// <summary>Models are authored large and scaled down so the player (eye height 1.7 m) can see over the pawns.</summary>
    public const float Scale = 0.62f;

    /// <summary>Radius of a piece's collision cylinder, shared by the client and the server.</summary>
    public const float ColliderRadius = 1.0f * Scale;

    public static float Height(PieceType type) => Scale * type switch
    {
        PieceType.Pawn => 2.4f,
        PieceType.Knight => 3.2f,
        PieceType.Bishop => 3.7f,
        PieceType.Rook => 3.0f,
        PieceType.Queen => 4.1f,
        _ => 4.7f,
    };

    public static Node3D Build(Piece piece)
    {
        var root = new Node3D { Name = $"{piece.Color}{piece.Type}", Scale = new Vector3(Scale, Scale, Scale) };
        var material = Material(piece.Color);
        void Add(Mesh mesh, Vector3 position, Vector3? rotationDegrees = null, Vector3? scale = null)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotationDegrees ?? Vector3.Zero,
                Scale = scale ?? Vector3.One,
                MaterialOverride = material,
            });
        }

        // Shared pedestal.
        Add(Cylinder(0.95f, 1.1f, 0.3f), new Vector3(0, 0.15f, 0));
        Add(Torus(0.7f, 1.0f), new Vector3(0, 0.36f, 0));

        switch (piece.Type)
        {
            case PieceType.Pawn:
                Add(Cylinder(0.32f, 0.7f, 1.2f), new Vector3(0, 1.0f, 0));
                Add(Cylinder(0.62f, 0.62f, 0.12f), new Vector3(0, 1.62f, 0));
                Add(Sphere(0.48f), new Vector3(0, 2.0f, 0));
                break;

            case PieceType.Rook:
                Add(Cylinder(0.62f, 0.8f, 1.9f), new Vector3(0, 1.4f, 0));
                Add(Cylinder(0.88f, 0.75f, 0.45f), new Vector3(0, 2.55f, 0));
                for (var i = 0; i < 4; i++)
                {
                    var angle = Mathf.Pi / 2f * i + Mathf.Pi / 4f;
                    Add(new BoxMesh { Size = new Vector3(0.42f, 0.35f, 0.42f) },
                        new Vector3(Mathf.Cos(angle) * 0.62f, 2.95f, Mathf.Sin(angle) * 0.62f),
                        new Vector3(0, -Mathf.RadToDeg(angle), 0));
                }
                break;

            case PieceType.Knight:
                Add(Cylinder(0.55f, 0.8f, 1.1f), new Vector3(0, 0.95f, 0));
                // Neck leaning forward, head pointing towards -Z, ears and mane on top.
                Add(new BoxMesh { Size = new Vector3(0.75f, 1.5f, 0.9f) }, new Vector3(0, 2.05f, 0.1f), new Vector3(-18, 0, 0));
                Add(new BoxMesh { Size = new Vector3(0.7f, 0.6f, 1.25f) }, new Vector3(0, 2.6f, -0.45f), new Vector3(12, 0, 0));
                Add(new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.55f) }, new Vector3(0, 2.32f, -1.0f), new Vector3(20, 0, 0));
                Add(new PrismMesh { Size = new Vector3(0.22f, 0.4f, 0.22f) }, new Vector3(0.2f, 3.05f, -0.15f));
                Add(new PrismMesh { Size = new Vector3(0.22f, 0.4f, 0.22f) }, new Vector3(-0.2f, 3.05f, -0.15f));
                Add(new BoxMesh { Size = new Vector3(0.2f, 1.5f, 0.3f) }, new Vector3(0, 2.25f, 0.55f), new Vector3(-18, 0, 0));
                break;

            case PieceType.Bishop:
                Add(Cylinder(0.32f, 0.75f, 2.0f), new Vector3(0, 1.4f, 0));
                Add(Cylinder(0.58f, 0.58f, 0.12f), new Vector3(0, 2.45f, 0));
                Add(Sphere(0.55f), new Vector3(0, 2.95f, 0), scale: new Vector3(1f, 1.4f, 1f));
                Add(Sphere(0.16f), new Vector3(0, 3.62f, 0));
                break;

            case PieceType.Queen:
                Add(Cylinder(0.38f, 0.82f, 2.5f), new Vector3(0, 1.65f, 0));
                Add(Torus(0.35f, 0.62f), new Vector3(0, 2.95f, 0));
                Add(Cylinder(0.78f, 0.42f, 0.7f), new Vector3(0, 3.35f, 0));
                for (var i = 0; i < 8; i++)
                {
                    var angle = Mathf.Tau / 8f * i;
                    Add(Sphere(0.12f), new Vector3(Mathf.Cos(angle) * 0.75f, 3.75f, Mathf.Sin(angle) * 0.75f));
                }
                Add(Sphere(0.24f), new Vector3(0, 3.85f, 0));
                break;

            default: // King
                Add(Cylinder(0.42f, 0.85f, 2.8f), new Vector3(0, 1.8f, 0));
                Add(Torus(0.38f, 0.66f), new Vector3(0, 3.25f, 0));
                Add(Cylinder(0.7f, 0.45f, 0.65f), new Vector3(0, 3.62f, 0));
                Add(new BoxMesh { Size = new Vector3(0.22f, 0.8f, 0.22f) }, new Vector3(0, 4.3f, 0));
                Add(new BoxMesh { Size = new Vector3(0.6f, 0.2f, 0.22f) }, new Vector3(0, 4.4f, 0));
                break;
        }

        if (piece.Color == PieceColor.Black) root.RotationDegrees = new Vector3(0, 180, 0);
        return root;
    }

    private static CylinderMesh Cylinder(float top, float bottom, float height) =>
        new() { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 40 };

    private static SphereMesh Sphere(float radius) =>
        new() { Radius = radius, Height = radius * 2f, RadialSegments = 32, Rings = 16 };

    private static TorusMesh Torus(float inner, float outer) =>
        new() { InnerRadius = inner, OuterRadius = outer, Rings = 32, RingSegments = 16 };
}
