using Godot;

namespace FpsChess.Fps;

/// <summary>The first-person weapon model held in front of the camera. Purely visual.</summary>
public partial class Blaster : Node3D
{
    private static readonly Vector3 RestPosition = new(0.2f, -0.2f, -0.5f);
    private static readonly Color Glow = new(0.35f, 0.85f, 1f);

    private Node3D _muzzle = null!;

    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;

    public override void _Ready()
    {
        Position = RestPosition;
        Scale = new Vector3(0.45f, 0.45f, 0.45f);
        var body = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.17f, 0.2f), Metallic = 0.7f, Roughness = 0.3f };
        var accent = new StandardMaterial3D
        {
            AlbedoColor = Glow,
            EmissionEnabled = true,
            Emission = Glow,
            EmissionEnergyMultiplier = 0.8f,
        };

        void Part(Mesh mesh, Vector3 position, Material material, Vector3? rotation = null) => AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            RotationDegrees = rotation ?? Vector3.Zero,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        Part(new BoxMesh { Size = new Vector3(0.12f, 0.14f, 0.45f) }, Vector3.Zero, body);
        Part(new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.32f }, new Vector3(0, 0.02f, -0.36f), body, new Vector3(90, 0, 0));
        Part(new BoxMesh { Size = new Vector3(0.09f, 0.2f, 0.1f) }, new Vector3(0, -0.14f, 0.1f), body, new Vector3(-15, 0, 0));
        Part(new BoxMesh { Size = new Vector3(0.125f, 0.015f, 0.25f) }, new Vector3(0, 0.075f, -0.02f), accent);
        Part(new TorusMesh { InnerRadius = 0.04f, OuterRadius = 0.06f }, new Vector3(0, 0.02f, -0.48f), accent, new Vector3(90, 0, 0));

        _muzzle = new Node3D { Position = new Vector3(0, 0.02f, -0.55f) };
        AddChild(_muzzle);
    }

    public void Fire(Node3D world, Vector3 target)
    {
        Effects.Tracer(world, MuzzlePosition, target, Glow);
        var tween = CreateTween();
        tween.TweenProperty(this, "position", RestPosition + new Vector3(0, 0.02f, 0.08f), 0.05f);
        tween.TweenProperty(this, "position", RestPosition, 0.2f).SetTrans(Tween.TransitionType.Sine);
    }
}
