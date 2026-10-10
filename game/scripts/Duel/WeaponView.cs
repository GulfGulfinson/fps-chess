using Chess.Core;
using Godot;

namespace FpsChess.Duel;

/// <summary>The first-person weapon in front of the camera, a different model per piece class. Purely visual.</summary>
public partial class WeaponView : Node3D
{
    private static readonly Vector3 RestPosition = new(0.22f, -0.22f, -0.5f);

    // Aiming down the sights pulls the weapon to the centre, just below the crosshair.
    private static readonly Vector3 AimPosition = new(0f, -0.13f, -0.42f);

    private Node3D _muzzle = null!;
    private MeshInstance3D _flash = null!;
    private float _kick;
    private float _reloadLeft;
    private float _reloadDuration = 1f;
    private bool _aiming;
    private float _moveSpeed;
    private float _bobPhase;
    private float _flashLeft;
    private Node3D? _barrels;
    private PieceType _type;
    private Color _glow;

    public Vector3 MuzzlePosition => _muzzle.GlobalPosition;

    public void Init(PieceType type, Color glow)
    {
        _type = type;
        _glow = glow;
    }

    public override void _Ready()
    {
        Position = RestPosition;
        Scale = new Vector3(0.45f, 0.45f, 0.45f);
        var body = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.16f, 0.19f), Metallic = 0.7f, Roughness = 0.3f };
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.22f, 0.12f), Roughness = 0.7f };
        var accent = new StandardMaterial3D { AlbedoColor = _glow, EmissionEnabled = true, Emission = _glow, EmissionEnergyMultiplier = 0.8f };

        Node3D Part(Mesh mesh, Vector3 position, Material material, Vector3? rotation = null, Node3D? parent = null)
        {
            var part = new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation ?? Vector3.Zero,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            (parent ?? this).AddChild(part);
            return part;
        }

        var barrelForward = new Vector3(90, 0, 0);
        float muzzleZ;
        switch (_type)
        {
            case PieceType.Knight: // double-barrelled shotgun
                Part(new BoxMesh { Size = new Vector3(0.16f, 0.13f, 0.4f) }, new Vector3(0, 0, 0.05f), wood);
                Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.6f }, new Vector3(-0.04f, 0.03f, -0.4f), body, barrelForward);
                Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.6f }, new Vector3(0.04f, 0.03f, -0.4f), body, barrelForward);
                muzzleZ = -0.72f;
                break;
            case PieceType.Bishop: // sniper rifle with scope
                Part(new BoxMesh { Size = new Vector3(0.1f, 0.12f, 0.6f) }, Vector3.Zero, body);
                Part(new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.7f }, new Vector3(0, 0.02f, -0.62f), body, barrelForward);
                Part(new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.045f, Height = 0.32f }, new Vector3(0, 0.13f, -0.05f), body, barrelForward);
                Part(new TorusMesh { InnerRadius = 0.035f, OuterRadius = 0.05f }, new Vector3(0, 0.13f, -0.22f), accent, barrelForward);
                muzzleZ = -0.98f;
                break;
            case PieceType.Rook: // minigun
                Part(new BoxMesh { Size = new Vector3(0.22f, 0.2f, 0.4f) }, new Vector3(0, -0.02f, 0.1f), body);
                _barrels = new Node3D { Position = new Vector3(0, 0, -0.35f) };
                AddChild(_barrels);
                for (var i = 0; i < 6; i++)
                {
                    var angle = Mathf.Tau / 6f * i;
                    Part(new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.022f, Height = 0.6f },
                        new Vector3(Mathf.Cos(angle) * 0.07f, Mathf.Sin(angle) * 0.07f, 0), body, barrelForward, _barrels);
                }
                Part(new TorusMesh { InnerRadius = 0.08f, OuterRadius = 0.1f }, new Vector3(0, 0, -0.3f), accent, barrelForward, _barrels);
                muzzleZ = -0.68f;
                break;
            case PieceType.Queen: // assault rifle with magazine
                Part(new BoxMesh { Size = new Vector3(0.11f, 0.14f, 0.5f) }, Vector3.Zero, body);
                Part(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.3f }, new Vector3(0, 0.02f, -0.38f), body, barrelForward);
                Part(new BoxMesh { Size = new Vector3(0.07f, 0.22f, 0.1f) }, new Vector3(0, -0.15f, -0.05f), body, new Vector3(-12, 0, 0));
                Part(new BoxMesh { Size = new Vector3(0.115f, 0.015f, 0.3f) }, new Vector3(0, 0.077f, 0), accent);
                muzzleZ = -0.55f;
                break;
            case PieceType.King: // revolver
                Part(new BoxMesh { Size = new Vector3(0.08f, 0.1f, 0.22f) }, Vector3.Zero, body);
                Part(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.1f }, new Vector3(0, 0.01f, -0.08f), accent, barrelForward);
                Part(new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.3f }, new Vector3(0, 0.03f, -0.27f), body, barrelForward);
                Part(new BoxMesh { Size = new Vector3(0.07f, 0.18f, 0.08f) }, new Vector3(0, -0.12f, 0.08f), wood, new Vector3(-20, 0, 0));
                muzzleZ = -0.43f;
                break;
            default: // pawn: semi-automatic rifle
                Part(new BoxMesh { Size = new Vector3(0.1f, 0.13f, 0.55f) }, Vector3.Zero, body);
                Part(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 0.35f }, new Vector3(0, 0.02f, -0.43f), body, barrelForward);
                Part(new BoxMesh { Size = new Vector3(0.08f, 0.18f, 0.1f) }, new Vector3(0, -0.13f, 0.12f), wood, new Vector3(-15, 0, 0));
                muzzleZ = -0.62f;
                break;
        }

        _muzzle = new Node3D { Position = new Vector3(0, 0.02f, muzzleZ) };
        AddChild(_muzzle);
        _flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1f, 0.85f, 0.5f),
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        _muzzle.AddChild(_flash);
    }

    public void SetAiming(bool aiming) => _aiming = aiming;

    /// <summary>Horizontal speed of the fighter, for the walking bob.</summary>
    public void SetMovement(float speed) => _moveSpeed = speed;

    /// <summary>The weapon dips and tilts for the length of the reload.</summary>
    public void PlayReload(float seconds)
    {
        _reloadDuration = Math.Max(0.1f, seconds);
        _reloadLeft = _reloadDuration;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _kick = Mathf.MoveToward(_kick, 0f, dt * 1.2f);
        _reloadLeft = Mathf.Max(0f, _reloadLeft - dt);
        _flashLeft -= dt;
        _flash.Visible = _flashLeft > 0f;

        var target = _aiming ? AimPosition : RestPosition;
        if (_moveSpeed > 0.5f && !_aiming)
        {
            _bobPhase += dt * (6f + _moveSpeed * 0.6f);
            target += new Vector3(Mathf.Cos(_bobPhase * 0.5f) * 0.012f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.018f, 0);
        }
        var reload = _reloadLeft > 0f ? Mathf.Sin(Mathf.Pi * (1f - _reloadLeft / _reloadDuration)) : 0f;
        target += new Vector3(0, -0.22f * reload + _kick * 0.3f, _kick);

        Position = Position.Lerp(target, Mathf.Min(1f, dt * 18f));
        Rotation = new Vector3(Mathf.DegToRad(_kick * 120f - reload * 35f), 0, Mathf.DegToRad(reload * 20f));
    }

    /// <summary>Spins the minigun barrels while it winds up.</summary>
    public void SetSpin(float normalized)
    {
        _barrels?.RotateZ(normalized * 0.6f);
    }

    public void Recoil()
    {
        _kick = Math.Min(0.12f, _kick + _type switch
        {
            PieceType.Bishop or PieceType.Knight => 0.09f,
            PieceType.King => 0.07f,
            PieceType.Rook => 0.01f,
            _ => 0.035f,
        });
        _flashLeft = 0.05f;
    }
}
