using Chess.Core;
using FpsChess.App;
using Godot;

namespace FpsChess.Arena;

/// <summary>
/// Builds the table with the board and the theme's obstacles. The server builds colliders only;
/// the client also builds meshes, the room around the table, lights and the environment.
/// Both sides call the same code, so what a player sees is exactly what the server's raycasts hit.
/// </summary>
public sealed class ArenaBuilder
{
    private readonly Node3D _root;
    private readonly bool _visuals;
    private readonly ArenaTheme _theme;

    private ArenaBuilder(Node3D root, ArenaTheme theme, bool visuals)
    {
        _root = root;
        _theme = theme;
        _visuals = visuals;
    }

    /// <returns>The static body of the table and board, used to map hit points to squares.</returns>
    public static StaticBody3D Build(Node3D root, ArenaThemeId themeId, bool visuals)
    {
        var builder = new ArenaBuilder(root, ArenaTheme.Get(themeId), visuals);
        var table = builder.BuildTable();
        builder.BuildObstacles();
        if (visuals)
        {
            builder.BuildBoardVisuals(table);
            builder.BuildRoom();
        }
        return table;
    }

    // ---------------------------------------------------------------- table and board

    private StaticBody3D BuildTable()
    {
        var table = new StaticBody3D { Name = "Table", CollisionLayer = BoardGeometry.WorldLayer, CollisionMask = 0 };
        var size = BoardGeometry.HalfTable * 2f;
        table.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size, 2f, size) }, Position = new Vector3(0, -1f, 0) });
        if (_visuals)
        {
            table.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(size, 2f, size) },
                Position = new Vector3(0, -1.02f, 0),
                MaterialOverride = Material(_theme.Table, 0.55f),
            });
            // Legs reach down to the room floor far below.
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
            {
                table.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(3f, 44f, 3f) },
                    Position = new Vector3(x * (BoardGeometry.HalfTable - 3f), -24f, z * (BoardGeometry.HalfTable - 3f)),
                    MaterialOverride = Material(_theme.Table.Darkened(0.2f), 0.6f),
                });
            }
        }
        _root.AddChild(table);
        return table;
    }

    private void BuildBoardVisuals(StaticBody3D table)
    {
        var light = Material(new Color(0.86f, 0.8f, 0.68f), 0.35f);
        var dark = Material(new Color(0.25f, 0.16f, 0.11f), 0.35f);
        var tile = new BoxMesh { Size = new Vector3(BoardGeometry.Tile, 0.3f, BoardGeometry.Tile) };
        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            table.AddChild(new MeshInstance3D
            {
                Mesh = tile,
                Position = BoardGeometry.Center(square) + new Vector3(0, -0.13f, 0),
                MaterialOverride = square.IsLight ? light : dark,
            });
        }

        var frame = Material(new Color(0.17f, 0.1f, 0.06f), 0.5f);
        var edge = BoardGeometry.HalfBoard + 0.5f;
        var length = BoardGeometry.HalfBoard * 2f + 2f;
        foreach (var (position, size) in new[]
                 {
                     (new Vector3(0, 0, edge), new Vector3(length, 0.3f, 1f)),
                     (new Vector3(0, 0, -edge), new Vector3(length, 0.3f, 1f)),
                     (new Vector3(edge, 0, 0), new Vector3(1f, 0.3f, length - 2f)),
                     (new Vector3(-edge, 0, 0), new Vector3(1f, 0.3f, length - 2f)),
                 })
        {
            table.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position + new Vector3(0, -0.13f, 0), MaterialOverride = frame });
        }

        for (var i = 0; i < 8; i++)
        {
            var x = (i - 3.5f) * BoardGeometry.Tile;
            table.AddChild(Label(((char)('a' + i)).ToString(), new Vector3(x, 0.04f, edge), 0));
            table.AddChild(Label(((char)('a' + i)).ToString(), new Vector3(x, 0.04f, -edge), 180));
            table.AddChild(Label((i + 1).ToString(), new Vector3(-edge, 0.04f, -x), 0));
            table.AddChild(Label((i + 1).ToString(), new Vector3(edge, 0.04f, -x), 180));
        }
    }

    private static Label3D Label(string text, Vector3 position, float yaw) => new()
    {
        Text = text,
        Font = Ui.UiTheme.Bold,
        FontSize = 150,
        PixelSize = 0.004f,
        Modulate = new Color(0.95f, 0.85f, 0.65f),
        Position = position,
        RotationDegrees = new Vector3(-90, yaw, 0),
    };

    // ---------------------------------------------------------------- room (client only)

    private void BuildRoom()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = _theme.Walls.Darkened(0.6f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = _theme.Ambient,
            AmbientLightEnergy = 0.8f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            GlowEnabled = GraphicsQuality.Glow,
            GlowIntensity = 0.5f,
            // No SSAO: it costs a large share of the frame on integrated GPUs for little visible gain here.
            FogEnabled = true,
            FogLightColor = _theme.Walls.Darkened(0.4f),
            FogDensity = 0.003f,
        };
        _root.AddChild(new WorldEnvironment { Environment = environment });
        _root.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -30, 0),
            LightColor = _theme.KeyLight,
            LightEnergy = _theme.KeyEnergy,
            ShadowEnabled = GraphicsQuality.Shadows,
            DirectionalShadowMode = GraphicsQuality.ShadowSplits,
            DirectionalShadowMaxDistance = 90f,
        });

        const float roomHalf = 130f;
        const float floorY = -46f;
        var walls = Material(_theme.Walls, 0.9f);
        _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(roomHalf * 2, 1, roomHalf * 2) }, Position = new Vector3(0, floorY - 0.5f, 0), MaterialOverride = Material(_theme.Floor, 0.8f) });
        foreach (var (position, size) in new[]
                 {
                     (new Vector3(0, 30, roomHalf), new Vector3(roomHalf * 2, 160, 2)),
                     (new Vector3(0, 30, -roomHalf), new Vector3(roomHalf * 2, 160, 2)),
                     (new Vector3(roomHalf, 30, 0), new Vector3(2, 160, roomHalf * 2)),
                     (new Vector3(-roomHalf, 30, 0), new Vector3(2, 160, roomHalf * 2)),
                 })
        {
            _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = walls });
        }

        // Lamps over the table give each theme its mood.
        foreach (var (x, z) in new[] { (-22f, -22f), (22f, -22f), (-22f, 22f), (22f, 22f) })
        {
            _root.AddChild(new OmniLight3D { Position = new Vector3(x, 28, z), LightColor = _theme.LampLight, LightEnergy = 2.5f, OmniRange = 70f });
        }

        switch (_theme.Id)
        {
            case ArenaThemeId.Library:
                // Bookshelves along the walls.
                var shelf = Material(new Color("4a2f1c"), 0.7f);
                for (var i = -3; i <= 3; i++)
                {
                    _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(30, 90, 10) }, Position = new Vector3(i * 34, 0, -roomHalf + 6), MaterialOverride = shelf });
                    for (var row = 0; row < 5; row++)
                    {
                        _root.AddChild(new MeshInstance3D
                        {
                            Mesh = new BoxMesh { Size = new Vector3(26, 12, 2) },
                            Position = new Vector3(i * 34, -36 + row * 17, -roomHalf + 11.2f),
                            MaterialOverride = Material(Color.FromHsv((i * 0.13f + row * 0.21f) % 1f, 0.55f, 0.45f), 0.8f),
                        });
                    }
                }
                break;
            case ArenaThemeId.CastleHall:
                // Pillars and banners.
                var stone = Material(new Color("5b5b60"), 0.95f);
                for (var i = -2; i <= 2; i++)
                {
                    _root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 5, BottomRadius = 6, Height = 160 }, Position = new Vector3(i * 50, 30, -roomHalf + 12), MaterialOverride = stone });
                    _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(14, 40, 0.5f) }, Position = new Vector3(i * 50 + 25, 20, -roomHalf + 2), MaterialOverride = Material(i % 2 == 0 ? new Color("8c1d1d") : new Color("1d3a8c"), 0.9f) });
                }
                break;
            default:
                // Glowing panels on the walls.
                for (var i = -3; i <= 3; i++)
                {
                    _root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(24, 4, 1) }, Position = new Vector3(i * 34, 20, -roomHalf + 2), MaterialOverride = Glow(new Color("6fd3ff"), 2f) });
                }
                break;
        }
    }

    // ---------------------------------------------------------------- obstacles

    private void BuildObstacles()
    {
        switch (_theme.Id)
        {
            case ArenaThemeId.Library:
                BookStack(new Vector3(-20, 0, -8), 15);
                BookStack(new Vector3(22, 0, 2), -25);
                Inkwell(new Vector3(18, 0, -18));
                Candle(new Vector3(-17, 0, 18));
                OpenBook(new Vector3(19, 0, 18), 20);
                Teacup(new Vector3(-4, 0, -22));
                StandingBooks(new Vector3(-26, 0, 2));
                Teacup(new Vector3(6, 0, 22));
                break;

            case ArenaThemeId.CastleHall:
                Goblet(new Vector3(-19, 0, -10));
                Goblet(new Vector3(16, 0, 21));
                Candelabra(new Vector3(19, 0, -17));
                Dagger(new Vector3(-14, 0, 20), 30);
                Scroll(new Vector3(21, 0, 8), 80);
                CoinStack(new Vector3(-24, 0, 6), 5);
                CoinStack(new Vector3(5, 0, -22), 3);
                Shield(new Vector3(-3, 0, 23));
                break;

            default:
                Beaker(new Vector3(-19, 0, -12), new Color("49ff8a"));
                Beaker(new Vector3(14, 0, 22), new Color("ff5ad1"));
                DataCube(new Vector3(18, 0, -18));
                DataCube(new Vector3(22, 0, -13));
                Microscope(new Vector3(21, 0, 12));
                Cable(new Vector3(-18, 0, 19));
                Monitor(new Vector3(0, 0, -24));
                RoundFlask(new Vector3(-25, 0, 3));
                break;
        }
    }

    private StaticBody3D Obstacle(string name, Vector3 position, float yawDegrees = 0f)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(0, yawDegrees, 0),
            CollisionLayer = BoardGeometry.WorldLayer,
            CollisionMask = 0,
        };
        _root.AddChild(body);
        return body;
    }

    /// <summary>Adds a collider and, on the client, the matching mesh.</summary>
    private void Part(Node3D body, Shape3D shape, Mesh mesh, Vector3 position, Material material, Vector3? rotation = null)
    {
        var rotationDegrees = rotation ?? Vector3.Zero;
        body.AddChild(new CollisionShape3D { Shape = shape, Position = position, RotationDegrees = rotationDegrees });
        if (_visuals) body.AddChild(new MeshInstance3D { Mesh = mesh, Position = position, RotationDegrees = rotationDegrees, MaterialOverride = material });
    }

    private void Box(Node3D body, Vector3 size, Vector3 position, Material material, Vector3? rotation = null) =>
        Part(body, new BoxShape3D { Size = size }, new BoxMesh { Size = size }, position, material, rotation);

    private void Cylinder(Node3D body, float radius, float height, Vector3 position, Material material, Vector3? rotation = null, float? topRadius = null) =>
        Part(body, new CylinderShape3D { Radius = Math.Max(radius, topRadius ?? 0f), Height = height },
            new CylinderMesh { TopRadius = topRadius ?? radius, BottomRadius = radius, Height = height, RadialSegments = 32 },
            position, material, rotation);

    private void Decoration(Node3D body, Mesh mesh, Vector3 position, Material material, Vector3? rotation = null)
    {
        if (_visuals) body.AddChild(new MeshInstance3D { Mesh = mesh, Position = position, RotationDegrees = rotation ?? Vector3.Zero, MaterialOverride = material });
    }

    private void Lamp(Node3D body, Vector3 position, Color color, float energy = 3f)
    {
        if (!_visuals) return;
        body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 1.2f }, Position = position, MaterialOverride = Glow(color, 4f) });
        body.AddChild(new OmniLight3D { Position = position, LightColor = color, LightEnergy = energy, OmniRange = 18f });
    }

    private void BookStack(Vector3 position, float yaw)
    {
        var body = Obstacle("BookStack", position, yaw);
        var colors = new[] { new Color("7a1f1f"), new Color("1f5a3a"), new Color("6b4a1f") };
        for (var i = 0; i < 3; i++)
        {
            Box(body, new Vector3(7f - i * 0.4f, 1.2f, 5f - i * 0.2f), new Vector3(i * 0.3f, 0.6f + i * 1.2f, 0), Material(colors[i], 0.7f), new Vector3(0, i * 9f, 0));
        }
    }

    private void Inkwell(Vector3 position)
    {
        var body = Obstacle("Inkwell", position);
        Cylinder(body, 2.2f, 3f, new Vector3(0, 1.5f, 0), Material(new Color("1a2440"), 0.1f, 0.3f), topRadius: 1.4f);
        Decoration(body, new BoxMesh { Size = new Vector3(0.2f, 9f, 0.6f) }, new Vector3(0.6f, 5f, 0), Material(new Color("f2efe6"), 0.6f), new Vector3(0, 0, -20));
    }

    private void Candle(Vector3 position)
    {
        var body = Obstacle("Candle", position);
        Cylinder(body, 1.8f, 0.5f, new Vector3(0, 0.25f, 0), Material(new Color("b08d57"), 0.3f, 0.8f));
        Cylinder(body, 1.1f, 7f, new Vector3(0, 4f, 0), Material(new Color("f3e9d2"), 0.8f));
        Lamp(body, new Vector3(0, 8f, 0), new Color("ffb04a"), 4f);
    }

    private void OpenBook(Vector3 position, float yaw)
    {
        var body = Obstacle("OpenBook", position, yaw);
        var pages = Material(new Color("efe6d0"), 0.9f);
        Box(body, new Vector3(5f, 0.6f, 7f), new Vector3(-2.4f, 0.5f, 0), pages, new Vector3(0, 0, 8));
        Box(body, new Vector3(5f, 0.6f, 7f), new Vector3(2.4f, 0.5f, 0), pages, new Vector3(0, 0, -8));
    }

    private void Teacup(Vector3 position)
    {
        var body = Obstacle("Teacup", position);
        var porcelain = Material(new Color("f4f4f6"), 0.25f);
        Cylinder(body, 3.4f, 0.3f, new Vector3(0, 0.15f, 0), porcelain);
        Cylinder(body, 1.7f, 3.2f, new Vector3(0, 1.9f, 0), porcelain, topRadius: 2.4f);
        Decoration(body, new TorusMesh { InnerRadius = 0.6f, OuterRadius = 1.1f }, new Vector3(2.6f, 2f, 0), porcelain, new Vector3(90, 0, 0));
    }

    private void StandingBooks(Vector3 position)
    {
        var body = Obstacle("StandingBooks", position);
        for (var i = 0; i < 5; i++)
        {
            Box(body, new Vector3(1.3f, 8f - i % 2, 6f), new Vector3(0, 4f - i % 2 * 0.5f, i * 1.4f - 2.8f),
                Material(Color.FromHsv(i * 0.17f % 1f, 0.6f, 0.4f), 0.7f));
        }
    }

    private void Goblet(Vector3 position)
    {
        var body = Obstacle("Goblet", position);
        var gold = Material(new Color("c9a24a"), 0.25f, 0.9f);
        Cylinder(body, 2f, 0.4f, new Vector3(0, 0.2f, 0), gold);
        Cylinder(body, 0.5f, 3f, new Vector3(0, 1.9f, 0), gold);
        Cylinder(body, 1f, 3f, new Vector3(0, 4.9f, 0), gold, topRadius: 2.3f);
    }

    private void Candelabra(Vector3 position)
    {
        var body = Obstacle("Candelabra", position);
        var brass = Material(new Color("a8843a"), 0.3f, 0.9f);
        Cylinder(body, 2.5f, 0.6f, new Vector3(0, 0.3f, 0), brass);
        Cylinder(body, 0.4f, 6f, new Vector3(0, 3.6f, 0), brass);
        Box(body, new Vector3(7f, 0.4f, 0.4f), new Vector3(0, 6.4f, 0), brass);
        foreach (var x in new[] { -3.3f, 0f, 3.3f })
        {
            Cylinder(body, 0.45f, 2f, new Vector3(x, 7.6f, 0), Material(new Color("f3e9d2"), 0.8f));
            Lamp(body, new Vector3(x, 8.9f, 0), new Color("ff9a3d"), 2f);
        }
    }

    private void Dagger(Vector3 position, float yaw)
    {
        var body = Obstacle("Dagger", position, yaw);
        Box(body, new Vector3(0.9f, 0.3f, 10f), new Vector3(0, 0.15f, -3f), Material(new Color("c7ccd4"), 0.15f, 1f));
        Box(body, new Vector3(4f, 0.9f, 0.9f), new Vector3(0, 0.45f, 2.2f), Material(new Color("8a6a2a"), 0.3f, 0.8f));
        Cylinder(body, 0.45f, 3.5f, new Vector3(0, 0.45f, 4.4f), Material(new Color("3a2414"), 0.8f), new Vector3(90, 0, 0));
    }

    private void Scroll(Vector3 position, float yaw)
    {
        var body = Obstacle("Scroll", position, yaw);
        Cylinder(body, 1.4f, 9f, new Vector3(0, 1.4f, 0), Material(new Color("e3d3a8"), 0.9f), new Vector3(0, 0, 90));
        Decoration(body, new TorusMesh { InnerRadius = 1.35f, OuterRadius = 1.6f }, new Vector3(0, 1.4f, 0), Material(new Color("8c1d1d"), 0.8f), new Vector3(0, 0, 90));
    }

    private void CoinStack(Vector3 position, int coins)
    {
        var body = Obstacle("Coins", position);
        for (var i = 0; i < coins; i++)
        {
            Cylinder(body, 2f, 0.5f, new Vector3(i * 0.15f, 0.25f + i * 0.5f, 0), Material(new Color("d4af37"), 0.3f, 1f));
        }
    }

    private void Shield(Vector3 position)
    {
        var body = Obstacle("Shield", position);
        Cylinder(body, 4f, 0.6f, new Vector3(0, 4f, 0), Material(new Color("8c1d1d"), 0.5f, 0.3f), new Vector3(90, 0, 0));
        Decoration(body, new TorusMesh { InnerRadius = 3.6f, OuterRadius = 4.1f }, new Vector3(0, 4f, 0), Material(new Color("c9a24a"), 0.3f, 0.9f), new Vector3(90, 0, 0));
    }

    private void Beaker(Vector3 position, Color liquid)
    {
        var body = Obstacle("Beaker", position);
        var glass = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.25f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f,
        };
        Cylinder(body, 2.5f, 6f, new Vector3(0, 3f, 0), glass);
        Decoration(body, new CylinderMesh { TopRadius = 2.3f, BottomRadius = 2.3f, Height = 3f }, new Vector3(0, 1.6f, 0), Glow(liquid, 1.2f));
    }

    private void DataCube(Vector3 position)
    {
        var body = Obstacle("DataCube", position, 20);
        Box(body, new Vector3(3f, 3f, 3f), new Vector3(0, 1.5f, 0), Material(new Color("1b2733"), 0.3f, 0.6f));
        Decoration(body, new BoxMesh { Size = new Vector3(3.1f, 0.25f, 3.1f) }, new Vector3(0, 2.2f, 0), Glow(new Color("4fc3ff"), 3f));
    }

    private void Microscope(Vector3 position)
    {
        var body = Obstacle("Microscope", position, -30);
        var white = Material(new Color("e8edf2"), 0.4f);
        Box(body, new Vector3(5f, 1f, 6f), new Vector3(0, 0.5f, 0), white);
        Box(body, new Vector3(1.5f, 7f, 1.5f), new Vector3(0, 4.5f, 2f), white);
        Cylinder(body, 0.8f, 5f, new Vector3(0, 7f, 0.2f), Material(new Color("2a3440"), 0.3f, 0.7f), new Vector3(35, 0, 0));
    }

    private void Cable(Vector3 position)
    {
        var body = Obstacle("Cable", position, 15);
        Cylinder(body, 0.5f, 16f, new Vector3(0, 0.5f, 0), Material(new Color("20252b"), 0.6f), new Vector3(0, 0, 90));
    }

    private void Monitor(Vector3 position)
    {
        var body = Obstacle("Monitor", position);
        var frame = Material(new Color("1c2128"), 0.4f, 0.4f);
        Box(body, new Vector3(4f, 0.4f, 3f), new Vector3(0, 0.2f, 0), frame);
        Box(body, new Vector3(0.8f, 3f, 0.8f), new Vector3(0, 1.9f, 0), frame);
        Box(body, new Vector3(12f, 7f, 0.8f), new Vector3(0, 6.9f, 0), frame);
        Decoration(body, new BoxMesh { Size = new Vector3(11f, 6f, 0.1f) }, new Vector3(0, 6.9f, 0.45f), Glow(new Color("2a7fff"), 1.5f));
    }

    private void RoundFlask(Vector3 position)
    {
        var body = Obstacle("RoundFlask", position);
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.3f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f };
        Part(body, new SphereShape3D { Radius = 3f }, new SphereMesh { Radius = 3f, Height = 6f }, new Vector3(0, 3f, 0), glass);
        Cylinder(body, 0.9f, 4f, new Vector3(0, 7.5f, 0), glass);
        Decoration(body, new SphereMesh { Radius = 2.6f, Height = 3f }, new Vector3(0, 1.6f, 0), Glow(new Color("ffb23f"), 1.2f));
    }

    // ---------------------------------------------------------------- materials

    private static StandardMaterial3D Material(Color color, float roughness, float metallic = 0f) =>
        new() { AlbedoColor = color, Roughness = roughness, Metallic = metallic };

    private static StandardMaterial3D Glow(Color color, float energy) => new()
    {
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = energy,
    };
}
