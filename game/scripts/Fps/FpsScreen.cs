using Chess.Core;
using Chess.Core.Session;
using FpsChess.App;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Fps;

/// <summary>
/// FPS mode: the same game, played by walking across a giant board. Aim at your piece and click to select it,
/// then click a highlighted square (or shoot an enemy piece) to move. The chess rules stay exactly the same.
/// </summary>
public partial class FpsScreen : Node3D
{
    private enum Highlight
    {
        None,
        LastMove,
        Check,
        Hover,
        Selected,
        Move,
        MoveHover,
        Capture,
        CaptureHover,
    }

    private static readonly Color WhiteShot = new(0.35f, 0.85f, 1f);
    private static readonly Color BlackShot = new(1f, 0.3f, 0.25f);

    private readonly Dictionary<Square, PieceActor> _actors = [];
    private readonly MeshInstance3D[] _overlays = new MeshInstance3D[64];
    private readonly Dictionary<Highlight, StandardMaterial3D> _highlightMaterials = [];

    private Main _main = null!;
    private GameSession _session = null!;
    private FpsPlayer _player = null!;
    private Node3D _pieces = null!;
    private Node3D _effects = null!;
    private StaticBody3D _boardBody = null!;
    private FpsHud _hud = null!;
    private PauseMenu _pause = null!;
    private PromotionPicker _promotion = null!;
    private GameOverDialog _gameOver = null!;

    private Square? _selected;
    private List<Move> _targets = [];
    private Square? _aimSquare;
    private List<Move> _pendingPromotion = [];
    private bool _humanShotFired;
    private PieceColor _viewSide;

    public void Init(Main main, GameSession session)
    {
        _main = main;
        _session = session;
    }

    public override void _Ready()
    {
        BuildEnvironment();
        BuildArena();
        BuildBoard();
        _pieces = new Node3D { Name = "Pieces" };
        AddChild(_pieces);
        _effects = new Node3D { Name = "Effects" };
        AddChild(_effects);
        SyncPieces();

        _player = new FpsPlayer { Name = "Player" };
        AddChild(_player);
        _viewSide = InitialViewSide();
        _player.TeleportTo(BoardGeometry.SpawnFor(_viewSide));

        BuildUi();

        _session.MovePlayed += OnMovePlayed;
        _session.PositionReset += OnPositionReset;
        _session.GameEnded += OnGameEnded;

        RefreshHighlights();
        RefreshMoveList();
        if (_session.Game.IsOver) OnGameEnded();
        else
        {
            CaptureMouse();
            _hud.ShowBanner($"Du spielst {Texts.Color(_viewSide)}", UiTheme.Accent);
        }
    }

    public override void _ExitTree()
    {
        _session.MovePlayed -= OnMovePlayed;
        _session.PositionReset -= OnPositionReset;
        _session.GameEnded -= OnGameEnded;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private PieceColor InitialViewSide()
    {
        var white = _session.Setup.White == PlayerKind.Human;
        var black = _session.Setup.Black == PlayerKind.Human;
        if (white && black) return _session.Turn;
        return black ? PieceColor.Black : PieceColor.White;
    }

    private bool IsHotSeat => _session.Setup is { White: PlayerKind.Human, Black: PlayerKind.Human };

    private bool OverlayOpen => _pause.Visible || _promotion.Visible || _gameOver.Visible;

    // ---------------------------------------------------------------- world

    private void BuildEnvironment()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.05f, 0.07f, 0.16f),
                    SkyHorizonColor = new Color(0.55f, 0.35f, 0.3f),
                    GroundHorizonColor = new Color(0.2f, 0.15f, 0.15f),
                    GroundBottomColor = new Color(0.03f, 0.03f, 0.04f),
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.7f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            SsaoEnabled = true,
            FogEnabled = true,
            FogLightColor = new Color(0.35f, 0.28f, 0.3f),
            FogDensity = 0.004f,
        };
        AddChild(new WorldEnvironment { Environment = environment });

        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -35, 0),
            LightColor = new Color(1f, 0.88f, 0.75f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90f,
        });
    }

    private void BuildArena()
    {
        var ground = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(240, 1, 240) }, Position = new Vector3(0, -0.5f, 0) });
        ground.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(240, 240) },
            Position = new Vector3(0, -0.01f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.075f, 0.09f), Roughness = 0.95f },
        });
        // Invisible walls keep the player near the board.
        foreach (var (position, size) in new[]
                 {
                     (new Vector3(0, 5, 55), new Vector3(110, 10, 1)),
                     (new Vector3(0, 5, -55), new Vector3(110, 10, 1)),
                     (new Vector3(55, 5, 0), new Vector3(1, 10, 110)),
                     (new Vector3(-55, 5, 0), new Vector3(1, 10, 110)),
                 })
        {
            ground.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = position });
        }
        AddChild(ground);

        var stone = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.15f, 0.17f), Roughness = 0.8f };
        var glow = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.7f, 0.35f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.25f),
            EmissionEnergyMultiplier = 3f,
        };
        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Tau / 8f * i + Mathf.Pi / 8f;
            var position = new Vector3(Mathf.Cos(angle) * 27f, 0, Mathf.Sin(angle) * 27f);
            var pillar = new StaticBody3D { Position = position, CollisionLayer = 1, CollisionMask = 0 };
            pillar.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 1.1f, Height = 9f }, Position = new Vector3(0, 4.5f, 0), MaterialOverride = stone });
            pillar.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.7f, Height = 1.4f }, Position = new Vector3(0, 9.6f, 0), MaterialOverride = glow });
            pillar.AddChild(new OmniLight3D { Position = new Vector3(0, 9.6f, 0), LightColor = new Color(1f, 0.65f, 0.35f), LightEnergy = 3f, OmniRange = 22f });
            pillar.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.1f, Height = 9f }, Position = new Vector3(0, 4.5f, 0) });
            AddChild(pillar);
        }
    }

    private void BuildBoard()
    {
        var size = BoardGeometry.HalfBoard * 2f;
        _boardBody = new StaticBody3D { Name = "Board", CollisionLayer = 1, CollisionMask = 0 };
        _boardBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size + 1.6f, 0.4f, size + 1.6f) }, Position = new Vector3(0, -0.2f, 0) });
        AddChild(_boardBody);

        var light = new StandardMaterial3D { AlbedoColor = new Color(0.86f, 0.8f, 0.68f), Roughness = 0.35f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.16f, 0.11f), Roughness = 0.35f };
        var tile = new BoxMesh { Size = new Vector3(BoardGeometry.Tile, 0.4f, BoardGeometry.Tile) };
        var overlay = new PlaneMesh { Size = new Vector2(BoardGeometry.Tile * 0.96f, BoardGeometry.Tile * 0.96f) };

        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            var center = BoardGeometry.Center(square);
            _boardBody.AddChild(new MeshInstance3D { Mesh = tile, Position = center + new Vector3(0, -0.2f, 0), MaterialOverride = square.IsLight ? light : dark });
            _overlays[index] = new MeshInstance3D
            {
                Mesh = overlay,
                Position = center + new Vector3(0, 0.02f, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            AddChild(_overlays[index]);
        }

        var frame = new StandardMaterial3D { AlbedoColor = new Color(0.17f, 0.1f, 0.06f), Roughness = 0.5f };
        foreach (var (position, extent) in new[]
                 {
                     (new Vector3(0, 0, BoardGeometry.HalfBoard + 0.4f), new Vector3(size + 1.6f, 0.42f, 0.8f)),
                     (new Vector3(0, 0, -BoardGeometry.HalfBoard - 0.4f), new Vector3(size + 1.6f, 0.42f, 0.8f)),
                     (new Vector3(BoardGeometry.HalfBoard + 0.4f, 0, 0), new Vector3(0.8f, 0.42f, size)),
                     (new Vector3(-BoardGeometry.HalfBoard - 0.4f, 0, 0), new Vector3(0.8f, 0.42f, size)),
                 })
        {
            _boardBody.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = extent }, Position = position + new Vector3(0, -0.21f, 0), MaterialOverride = frame });
        }

        for (var i = 0; i < 8; i++)
        {
            var file = ((char)('a' + i)).ToString();
            var rank = (i + 1).ToString();
            var x = (i - 3.5f) * BoardGeometry.Tile;
            var edge = BoardGeometry.HalfBoard + 0.4f;
            AddChild(CoordinateLabel(file, new Vector3(x, 0.03f, edge), 0));
            AddChild(CoordinateLabel(file, new Vector3(x, 0.03f, -edge), 180));
            AddChild(CoordinateLabel(rank, new Vector3(-edge, 0.03f, -x), 0));
            AddChild(CoordinateLabel(rank, new Vector3(edge, 0.03f, -x), 180));
        }
    }

    private static Label3D CoordinateLabel(string text, Vector3 position, float yaw) => new()
    {
        Text = text,
        Font = UiTheme.Bold,
        FontSize = 160,
        PixelSize = 0.004f,
        Modulate = new Color(0.95f, 0.85f, 0.65f),
        Position = position,
        RotationDegrees = new Vector3(-90, yaw, 0),
    };

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _hud = new FpsHud();
        layer.AddChild(_hud);

        _pause = new PauseMenu();
        layer.AddChild(_pause);
        _pause.ResumeRequested += ClosePause;
        _pause.UndoRequested += () =>
        {
            _session.Undo();
            ClosePause();
        };
        _pause.SaveRequested += () => _pause.ShowToast(_main.SaveCurrentGame());
        _pause.ResignRequested += () =>
        {
            ClosePause();
            _session.Resign();
        };
        _pause.MenuRequested += () => _main.ShowMenu();

        _promotion = new PromotionPicker();
        layer.AddChild(_promotion);
        _promotion.Picked += type =>
        {
            CaptureMouse();
            Execute(_pendingPromotion.First(m => m.Promotion == type));
        };
        _promotion.Cancelled += () =>
        {
            CaptureMouse();
            Deselect();
        };

        _gameOver = new GameOverDialog();
        layer.AddChild(_gameOver);
        _gameOver.RematchRequested += _main.Rematch;
        _gameOver.MenuRequested += () => _main.ShowMenu();
        _gameOver.Closed += CaptureMouse;
    }

    private void SyncPieces()
    {
        foreach (var actor in _actors.Values) actor.QueueFree();
        _actors.Clear();
        foreach (var (square, piece) in _session.Game.Position.Pieces())
        {
            var actor = PieceActor.Create(piece, square);
            _pieces.AddChild(actor);
            _actors[square] = actor;
        }
    }

    // ---------------------------------------------------------------- frame loop and input

    public override void _Process(double delta)
    {
        _session.Update(TimeSpan.FromSeconds(delta));
        UpdateAim();
        UpdateHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputSetup.Pause))
        {
            if (_pause.Visible) ClosePause();
            else if (!_promotion.Visible && !_gameOver.Visible) OpenPause();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true } click || OverlayOpen) return;

        if (!_player.ControlsEnabled)
        {
            CaptureMouse();
            return;
        }

        if (click.ButtonIndex == MouseButton.Left) OnPrimaryClick();
        else if (click.ButtonIndex == MouseButton.Right) Deselect();
    }

    private void CaptureMouse()
    {
        if (!OverlayOpen) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OpenPause()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _session.Paused = true;
        _pause.Open(_session.CanUndo, !_session.Game.IsOver);
    }

    private void ClosePause()
    {
        _pause.Visible = false;
        _session.Paused = false;
        CaptureMouse();
    }

    private void UpdateAim()
    {
        Square? square = null;
        if (_player.ControlsEnabled && _player.Aim() is { } hit)
        {
            square = hit.Collider switch
            {
                PieceActor actor => actor.Square,
                StaticBody3D body when body == _boardBody => BoardGeometry.SquareAt(hit.Point),
                _ => null,
            };
        }

        // Pieces often stand between the player and a target square; let the floor square behind them win
        // when it is a legal destination and the piece in front is not.
        if (_player.ControlsEnabled && _targets.Count > 0 && !IsTarget(square) &&
            _player.AimAtFloor() is { } floor && BoardGeometry.SquareAt(floor) is { } floorSquare && IsTarget(floorSquare))
        {
            square = floorSquare;
        }

        if (square != _aimSquare)
        {
            _aimSquare = square;
            RefreshHighlights();
        }

        var (mode, text, color) = DescribeAim(square);
        _hud.SetCrosshair(mode);
        _hud.SetAimInfo(text, color);
    }

    private bool IsTarget(Square? square) => square is { } s && _targets.Any(m => m.To == s);

    private (CrosshairMode, string, Color) DescribeAim(Square? square)
    {
        if (square is not { } target) return (CrosshairMode.Idle, "", UiTheme.Text);

        var position = _session.Game.Position;
        var piece = position.PieceAt(target);
        var move = _targets.FirstOrDefault(m => m.To == target);
        if (_targets.Any(m => m.To == target))
        {
            if (position.IsCapture(move))
            {
                var victimSquare = piece is null ? new Square(target.File, move.From.Rank) : target;
                return (CrosshairMode.Capture, $"Schießen: {Describe(position.PieceAt(victimSquare))} ({victimSquare})", new Color("ff6a5f"));
            }
            return (CrosshairMode.Move, $"Ziehen nach {target}", new Color("7dff9b"));
        }

        if (piece is { } p && p.Color == _session.Turn && _session.IsHumanTurn)
            return (CrosshairMode.Select, $"{Describe(p)} ({target}) {(target == _selected ? "abwählen" : "auswählen")}", new Color("ffd36b"));

        return (CrosshairMode.Idle, piece is { } other ? $"{Describe(other)} ({target})" : target.ToString(), UiTheme.Text);
    }

    private static string Describe(Piece? piece) => piece is { } p
        ? $"{(p.Color == PieceColor.White ? "Weißer" : "Schwarzer")} {Texts.Piece(p.Type)}"
            .Replace("Weißer Dame", "Weiße Dame").Replace("Schwarzer Dame", "Schwarze Dame")
        : "";

    private void UpdateHud()
    {
        var game = _session.Game;
        var status = Texts.TurnStatus(game, _session.IsComputerThinking);
        if (!game.IsOver && !IsHotSeat && _session.IsHumanTurn) status = "Du bist am Zug." + (game.Position.IsInCheck(_session.Turn) ? " Schach!" : "");
        _hud.SetStatus(status);

        if (_session.Clock is { } clock)
        {
            string Side(PieceColor color) =>
                $"{(!game.IsOver && _session.Turn == color ? "▶ " : "   ")}{Texts.Color(color)}  {Texts.Clock(clock.Remaining(color))}";
            _hud.SetClocks($"{Side(PieceColor.White)}\n{Side(PieceColor.Black)}");
        }
        else
        {
            _hud.SetClocks($"Weiß: {Texts.Player(_session.Setup, PieceColor.White)}\nSchwarz: {Texts.Player(_session.Setup, PieceColor.Black)}");
        }
    }

    // ---------------------------------------------------------------- selecting and moving

    private void OnPrimaryClick()
    {
        if (!_session.IsHumanTurn || _aimSquare is not { } square)
        {
            Deselect();
            return;
        }

        var moves = _targets.Where(m => m.To == square).ToList();
        if (moves.Count > 1)
        {
            _pendingPromotion = moves;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _promotion.Open(_session.Turn);
            return;
        }
        if (moves.Count == 1)
        {
            Execute(moves[0]);
            return;
        }

        if (_session.Game.Position.PieceAt(square)?.Color == _session.Turn && square != _selected) Select(square);
        else Deselect();
    }

    private void Select(Square square)
    {
        Deselect();
        _selected = square;
        _targets = _session.Game.LegalMovesFrom(square).ToList();
        if (_actors.TryGetValue(square, out var actor)) actor.SetSelected(true);
        RefreshHighlights();
    }

    private void Deselect()
    {
        if (_selected is { } previous && _actors.TryGetValue(previous, out var actor)) actor.SetSelected(false);
        _selected = null;
        _targets = [];
        RefreshHighlights();
    }

    private void Execute(Move move)
    {
        var position = _session.Game.Position;
        if (position.IsCapture(move))
        {
            var victimSquare = position.PieceAt(move.To) is null ? new Square(move.To.File, move.From.Rank) : move.To;
            if (_actors.TryGetValue(victimSquare, out var victim))
            {
                _player.Blaster.Fire(_effects, victim.Head);
                _humanShotFired = true;
            }
        }
        Deselect();
        if (!_session.TryPlayHumanMove(move)) _humanShotFired = false;
    }

    private void OnMovePlayed(PlayedMove played)
    {
        Deselect();
        if (!_actors.Remove(played.Move.From, out var mover))
        {
            // Should not happen, but never leave the 3D board out of sync with the game.
            GD.PushWarning($"No 3D piece on {played.Move.From}; rebuilding the board.");
            SyncPieces();
            return;
        }

        var delay = 0f;
        if (played.CapturedOn is { } capturedOn && _actors.Remove(capturedOn, out var victim))
        {
            if (!_humanShotFired)
                Effects.Tracer(_effects, mover.Head, victim.Head, played.Mover.Color == PieceColor.White ? WhiteShot : BlackShot);
            victim.Die();
            delay = 0.3f;
        }
        _humanShotFired = false;

        _actors[played.Move.To] = mover;
        var slide = mover.SlideTo(played.Move.To, hop: played.Mover.Type == PieceType.Knight, delay);
        if (played.Move.Promotion is { } promotion)
            slide.Finished += () => mover.SetPiece(new Piece(promotion, played.Mover.Color));

        if (played.RookMove is { } rook && _actors.Remove(rook.From, out var rookActor))
        {
            _actors[rook.To] = rookActor;
            rookActor.SlideTo(rook.To, hop: false, delay: 0.25f);
        }

        RefreshHighlights();
        RefreshMoveList();

        var game = _session.Game;
        if (!game.IsOver && game.Position.IsInCheck(_session.Turn))
            _hud.ShowBanner("SCHACH!", new Color("ff5a4f"));
        else if (!game.IsOver && IsHotSeat)
            _hud.ShowBanner($"{Texts.Color(_session.Turn)} ist am Zug", UiTheme.Accent);

        if (IsHotSeat && !game.IsOver) SwitchSideAfter(1.1f, _session.Turn);
    }

    /// <summary>Hot-seat: once the move animation has played, hand the camera to the other player's side.</summary>
    private void SwitchSideAfter(float seconds, PieceColor side) => After(seconds, () =>
    {
        if (_session.Turn != side || _viewSide == side) return;
        _viewSide = side;
        _player.TeleportTo(BoardGeometry.SpawnFor(side));
    });

    /// <summary>
    /// Runs an action later on a tween owned by this node, so it is dropped when the screen is freed
    /// (a SceneTreeTimer would still fire and touch the disposed screen).
    /// </summary>
    private void After(double seconds, Action action)
    {
        var tween = CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenCallback(Callable.From(action));
    }

    private void OnPositionReset()
    {
        Deselect();
        SyncPieces();
        RefreshHighlights();
        RefreshMoveList();
        if (IsHotSeat && _viewSide != _session.Turn)
        {
            _viewSide = _session.Turn;
            _player.TeleportTo(BoardGeometry.SpawnFor(_viewSide));
        }
    }

    private void OnGameEnded()
    {
        Deselect();
        After(1.2, () =>
        {
            if (!_session.Game.IsOver) return;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _pause.Visible = false;
            _gameOver.Open(Texts.Result(_session.Game));
        });
    }

    private void RefreshMoveList()
    {
        var lines = Texts.MoveListLines(_session.Game).ToList();
        _hud.SetMoves(lines.Count == 0 ? "noch keine" : string.Join("\n", lines.TakeLast(8)));
    }

    // ---------------------------------------------------------------- floor highlights

    private void RefreshHighlights()
    {
        var position = _session.Game.Position;
        Square? checkedKing = !_session.Game.IsOver || _session.Game.Status == GameStatus.Checkmate
            ? position.IsInCheck(position.SideToMove) ? position.KingSquare(position.SideToMove) : null
            : null;
        var last = _session.Game.LastMove;

        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            var aimed = square == _aimSquare;
            var target = _targets.FirstOrDefault(m => m.To == square);
            var isTarget = _targets.Any(m => m.To == square);

            var highlight = Highlight.None;
            if (last is { } l && (l.From == square || l.To == square)) highlight = Highlight.LastMove;
            if (aimed && _session.IsHumanTurn && position.PieceAt(square)?.Color == _session.Turn) highlight = Highlight.Hover;
            if (square == checkedKing) highlight = Highlight.Check;
            if (square == _selected) highlight = Highlight.Selected;
            if (isTarget)
            {
                var capture = position.IsCapture(target);
                highlight = (capture, aimed) switch
                {
                    (true, true) => Highlight.CaptureHover,
                    (true, false) => Highlight.Capture,
                    (false, true) => Highlight.MoveHover,
                    _ => Highlight.Move,
                };
            }

            _overlays[index].Visible = highlight != Highlight.None;
            if (highlight != Highlight.None) _overlays[index].MaterialOverride = HighlightMaterial(highlight);
        }
    }

    private StandardMaterial3D HighlightMaterial(Highlight highlight)
    {
        if (_highlightMaterials.TryGetValue(highlight, out var cached)) return cached;
        var color = highlight switch
        {
            Highlight.LastMove => new Color(1f, 0.85f, 0.2f, 0.3f),
            Highlight.Check => new Color(1f, 0.1f, 0.1f, 0.65f),
            Highlight.Hover => new Color(1f, 1f, 1f, 0.22f),
            Highlight.Selected => new Color(1f, 0.78f, 0.25f, 0.6f),
            Highlight.Move => new Color(0.25f, 0.95f, 0.45f, 0.4f),
            Highlight.MoveHover => new Color(0.45f, 1f, 0.6f, 0.75f),
            Highlight.Capture => new Color(1f, 0.25f, 0.2f, 0.5f),
            _ => new Color(1f, 0.45f, 0.35f, 0.85f),
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
        };
        _highlightMaterials[highlight] = material;
        return material;
    }
}
