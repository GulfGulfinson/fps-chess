using Godot;

namespace FpsChess.Arena;

/// <summary>Short-lived visual effects: laser tracers, muzzle flashes and debris bursts.</summary>
public static class Effects
{
    public static void Tracer(Node3D parent, Vector3 from, Vector3 to, Color color)
    {
        var length = from.DistanceTo(to);
        if (length < 0.01f) return;

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 4f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var beam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = length, RadialSegments = 8 },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(beam);
        beam.GlobalPosition = (from + to) / 2f;
        // CylinderMesh runs along Y: point Y from start to end.
        var up = (to - from).Normalized();
        var side = Mathf.Abs(up.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : up.Cross(Vector3.Up).Normalized();
        beam.GlobalBasis = new Basis(side, up, side.Cross(up));

        Flash(parent, from, color, 0.12f);
        Flash(parent, to, color, 0.25f);

        var tween = beam.CreateTween();
        tween.TweenProperty(material, "albedo_color:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(beam.QueueFree));
    }

    public static void Flash(Node3D parent, Vector3 at, Color color, float seconds)
    {
        var light = new OmniLight3D { LightColor = color, LightEnergy = 6f, OmniRange = 6f };
        parent.AddChild(light);
        light.GlobalPosition = at;
        var tween = light.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, seconds);
        tween.TweenCallback(Callable.From(light.QueueFree));
    }

    public static void Burst(Node3D parent, Vector3 at, Color color)
    {
        var particles = new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Amount = 60,
            Lifetime = 1.2f,
            Explosiveness = 0.95f,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 4f,
            InitialVelocityMax = 10f,
            Gravity = new Vector3(0, -14f, 0),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.18f, 0.18f, 0.18f),
                Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.4f },
            },
        };
        parent.AddChild(particles);
        particles.GlobalPosition = at;
        particles.Emitting = true;
        Flash(parent, at, new Color(1f, 0.6f, 0.3f), 0.4f);

        particles.Finished += particles.QueueFree;
    }
}
