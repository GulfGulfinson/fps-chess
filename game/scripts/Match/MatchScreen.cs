using Chess.Core;
using Chess.Core.Duels;
using Chess.Core.Persistence;
using Chess.Core.Session;
using FpsChess.App;
using FpsChess.Arena;
using FpsChess.Duel;
using FpsChess.Net;
using FpsChess.Net.Client;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Match;

/// <summary>
/// FPS Chess as seen by one player. The board phase shows the table from above; every capture flies the
/// camera into the player's own piece for a first-person duel. All decisions come from the server: this
/// screen keeps a mirror <see cref="Game"/> built from the server's messages and only sends requests.
/// </summary>
public partial class MatchScreen : Node3D, IMatchClient
{
    private enum Phase
    {
        Board,
        DuelIntro,
        Duel,
        DuelOutro,
    }

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
    private static readonly Color BlackShot = new(1f, 0.35f, 0.25f);

    private readonly Dictionary<Square, PieceActor> _actors = [];
    private readonly MeshInstance3D[] _overlays = new MeshInstance3D[64];
    private readonly Dictionary<Highlight, StandardMaterial3D> _highlightMaterials = [];
    private readonly Queue<Action> _afterDuel = new();
    private readonly List<(RigidBody3D Body, float Life)> _grenades = [];

    private Main _main = null!;
    private NetClient _client = null!;
    private PieceColor _myColor;
    private string? _lanHint;

    private Game? _game;
    private MatchStateDto? _state;
    private Phase _phase = Phase.Board;
    private bool _built;
    private double _whiteMs;
    private double _blackMs;
    private bool _timed;

    private Node3D _world = null!;
    private Node3D _pieces = null!;
    private Node3D _effects = null!;
    private StaticBody3D _table = null!;
    private BoardCamera _camera = null!;
    private BoardHud _hud = null!;
    private DuelHud _duelHud = null!;
    private PauseMenu _pause = null!;
    private PromotionPicker _promotion = null!;
    private GameOverDialog _gameOver = null!;

    private Square? _selected;
    private List<Move> _targets = [];
    private Square? _aimSquare;
    private List<Move> _pendingPromotion = [];

    // Duel state on this side. The server is authoritative; the local DuelState only gates our own weapon.
    private DuelStartDto? _duelInfo;
    private DuelRole _myRole;
    private DuelState? _localDuel;
    private LocalFighter? _fighter;
    private RemoteFighter? _opponent;
    private readonly List<PieceActor> _hiddenActors = [];
    private Node3D? _attackArrow;
    private VersusCard _versus = null!;
    private int _duelCounter;
    private float _serverAttackerHealth;
    private float _serverDefenderHealth;
    private float _serverTimeLeft = DuelState.TimeLimitSeconds;
    private float _serverCountdown = DuelState.CountdownSeconds;
    private float _serverIntro = NetConfig.DuelIntroSeconds;
    private float _sinceInput;
    private bool _triggerHeld;
    private bool _zoomed;

    /// <param name="lanHint">Shown while waiting for an opponent when this player hosts a LAN game.</param>
    public void Init(Main main, NetClient client, PieceColor myColor, string? lanHint)
    {
        _main = main;
        _client = client;
        _myColor = myColor;
        _lanHint = lanHint;
    }

    public override void _Ready()
    {
        _world = new Node3D { Name = "World" };
        AddChild(_world);
        _pieces = new Node3D { Name = "Pieces" };
        _world.AddChild(_pieces);
        _effects = new Node3D { Name = "Effects" };
        _world.AddChild(_effects);

        _camera = new BoardCamera { Current = true };
        _camera.FaceFrom(_myColor);
        AddChild(_camera);

        BuildUi();
        _hud.SetStatus("Verbinde ...");
        GraphicsQuality.ApplyTo(GetViewport());
    }

    public override void _ExitTree()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GraphicsQuality.ResetViewport(GetViewport());
    }

    private MatchChannel Channel => _client.Channel!;

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _hud = new BoardHud();
        layer.AddChild(_hud);
        _duelHud = new DuelHud { Visible = false };
        layer.AddChild(_duelHud);

        _versus = new VersusCard { Visible = false };
        layer.AddChild(_versus);

        _pause = new PauseMenu();
        layer.AddChild(_pause);
        _pause.ResumeRequested += ClosePause;
        _pause.SaveRequested += () => Channel.RpcId(1, MatchChannel.MethodName.RequestSave);
        _pause.ResignRequested += () =>
        {
            ClosePause();
            Channel.RpcId(1, MatchChannel.MethodName.Resign);
        };
        _pause.MenuRequested += () => _main.ShowMenu();

        _promotion = new PromotionPicker();
        layer.AddChild(_promotion);
        _promotion.Picked += type => Submit(_pendingPromotion.First(m => m.Promotion == type));
        _promotion.Cancelled += Deselect;

        _gameOver = new GameOverDialog();
        layer.AddChild(_gameOver);
        _gameOver.RematchRequested += _main.Rematch;
        _gameOver.MenuRequested += () => _main.ShowMenu();
    }

    private bool OverlayOpen => _pause.Visible || _promotion.Visible || _gameOver.Visible;

    private bool VsComputer => _state?.VsComputer ?? false;

    // ================================================================ server messages

    public void OnState(string json)
    {
        try
        {
            _state = NetJson.Read<MatchStateDto>(json);
            var game = GameSerializer.Replay(_state.StartFen, GameVariant.Duel, _state.Moves);
            if (_state.PendingMove is { } pending) game.MakeMove(Move.ParseUci(pending));
            if (_state is { ForfeitStatus: { } status, ForfeitedBy: { } loser })
            {
                var color = Enum.Parse<PieceColor>(loser);
                if (status == nameof(GameStatus.TimeForfeit)) game.ForfeitOnTime(color);
                else game.Resign(color);
            }
            _game = game;
        }
        catch (Exception e) when (e is InvalidDataException or InvalidSaveFileException or FormatException or InvalidOperationException or ArgumentException)
        {
            GD.PushError($"[client] bad state from server: {e}");
            _main.ShowMenu("Der Server hat einen ungültigen Spielstand geschickt.");
            return;
        }

        _timed = _state.InitialMs is > 0;
        _whiteMs = _state.WhiteMs ?? 0;
        _blackMs = _state.BlackMs ?? 0;

        if (!_built)
        {
            _built = true;
            var theme = Enum.TryParse<ArenaThemeId>(_state.Theme, out var id) ? id : ArenaThemeId.Library;
            _table = ArenaBuilder.Build(_world, theme, visuals: true);
            BuildOverlays();
            _hud.ShowBanner(ArenaTheme.Get(theme).Name, UiTheme.Accent);
        }

        if (_phase != Phase.Board && _game.PendingDuel is null)
        {
            _afterDuel.Clear();
            EndDuel();
        }
        SyncPieces();
        Deselect();
        RefreshMoveList();
        _hud.SetPlayers(PlayersText());
        _gameOver.SetRematchVisible(_state.VsComputer);
        if (_game.IsOver) ShowGameOver();
    }

    /// <summary>An online room exists before the second player arrives.</summary>
    private bool WaitingForOpponent => _state is { VsComputer: false } state && !(state.White.Connected && state.Black.Connected) &&
                                       (state.White.Name == "(wartet)" || state.Black.Name == "(wartet)");

    public void OnMovePlayed(string uci, int outcome)
    {
        if (_phase != Phase.Board)
        {
            _afterDuel.Enqueue(() => OnMovePlayed(uci, outcome));
            return;
        }
        if (_game is null) return;

        var move = Move.ParseUci(uci);
        var before = _game.Position;
        try
        {
            if (outcome >= 0)
            {
                if (_game.PendingDuel?.Move != move) _game.MakeMove(move);
                _game.ResolveDuel((DuelOutcome)outcome);
            }
            else
            {
                _game.MakeMove(move);
            }
        }
        catch (InvalidOperationException e)
        {
            // Our mirror drifted from the server; the next full state repairs it.
            GD.PushError($"[client] could not apply {uci}: {e.Message}");
            return;
        }

        var played = PlayedMove.Describe(before, move, _game.SanMoves[^1], outcome >= 0 ? (DuelOutcome)outcome : null);
        Animate(played);
        RefreshMoveList();
        RefreshHighlights();

        if (_game.IsOver)
        {
            ShowGameOver();
            return;
        }
        if (_game.Position.IsInCheck(_game.Position.SideToMove))
            _hud.ShowBanner(_game.Position.SideToMove == _myColor ? "Du stehst im Schach!" : "SCHACH!", new Color("ff5a4f"));
        else if (_game.Position.IsInCheck(_game.Position.SideToMove.Opponent()))
            _hud.ShowBanner("Ein König steht offen!", new Color("ff5a4f"));
    }

    public void OnDuelStarted(string json)
    {
        if (_game is null) return;
        DuelStartDto info;
        try
        {
            info = NetJson.Read<DuelStartDto>(json);
            var move = Move.ParseUci(info.Move);
            if (_game.PendingDuel is null) _game.MakeMove(move);
        }
        catch (Exception e) when (e is InvalidDataException or FormatException or InvalidOperationException)
        {
            GD.PushError($"[client] bad duel start: {e.Message}");
            return;
        }

        Deselect();
        _promotion.Visible = false;
        _pause.Visible = false;
        _duelInfo = info;
        _myRole = Enum.Parse<PieceColor>(info.AttackerColor) == _myColor ? DuelRole.Attacker : DuelRole.Defender;
        var attackerType = Enum.Parse<PieceType>(info.Attacker);
        var defenderType = Enum.Parse<PieceType>(info.Defender);
        _localDuel = new DuelState(attackerType, defenderType);
        _serverAttackerHealth = _localDuel.Attacker.Health;
        _serverDefenderHealth = _localDuel.Defender.Health;
        _serverIntro = NetConfig.DuelIntroSeconds;
        _serverCountdown = DuelState.CountdownSeconds;
        _serverTimeLeft = DuelState.TimeLimitSeconds;
        _triggerHeld = false;
        _zoomed = false;
        StartDuelIntro(Square.Parse(info.AttackerSquare), Square.Parse(info.DefenderSquare), attackerType, defenderType);
    }

    public void OnDuelSnapshot(Vector3 attacker, float attackerYaw, float attackerPitch, Vector3 defender, float defenderYaw, float defenderPitch, float[] stats)
    {
        if (_localDuel is null || stats.Length < SnapshotField.Count) return;
        _serverAttackerHealth = stats[SnapshotField.AttackerHealth];
        _serverDefenderHealth = stats[SnapshotField.DefenderHealth];
        _serverTimeLeft = stats[SnapshotField.TimeLeft];
        _serverCountdown = stats[SnapshotField.CountdownLeft];
        _serverIntro = stats[SnapshotField.IntroLeft];

        var (position, yaw) = _myRole == DuelRole.Attacker ? (defender, defenderYaw) : (attacker, attackerYaw);
        _opponent?.SetPose(position, yaw);
        if (_opponent is not null && _localDuel is not null)
        {
            var enemy = _myRole == DuelRole.Attacker ? _localDuel.Defender : _localDuel.Attacker;
            _opponent.SetHealth(_myRole == DuelRole.Attacker ? _serverDefenderHealth : _serverAttackerHealth, enemy.Class.MaxHealth);
        }
    }

    public void OnShotFired(int role, Vector3 origin, Vector3[] ends)
    {
        if ((DuelRole)role == _myRole || _opponent is null) return;
        var attackerColor = Enum.Parse<PieceColor>(_duelInfo!.AttackerColor);
        var shooterColor = (DuelRole)role == DuelRole.Attacker ? attackerColor : attackerColor.Opponent();
        var color = shooterColor == PieceColor.White ? WhiteShot : BlackShot;
        for (var i = 0; i < ends.Length; i++) Effects.Tracer(_effects, _opponent.Head, ends[i], color, flash: i == 0);
    }

    public void OnDamaged(int role, float amount)
    {
        if (_phase is not (Phase.Duel or Phase.DuelIntro)) return;
        // Our own hits already showed a marker the moment we fired.
        if ((DuelRole)role == _myRole) _duelHud.DamageFlash();
    }

    public void OnGrenadeThrown(int role, Vector3 origin, Vector3 velocity)
    {
        var grenade = new RigidBody3D
        {
            CollisionLayer = 0,
            CollisionMask = BoardGeometry.WorldLayer | BoardGeometry.PieceLayer,
            LinearVelocity = velocity,
            Mass = 0.5f,
        };
        grenade.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.25f } });
        grenade.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("3b4a2a"), EmissionEnabled = true, Emission = new Color("ff3b2a"), EmissionEnergyMultiplier = 0.6f },
        });
        _effects.AddChild(grenade);
        grenade.GlobalPosition = origin;
        _grenades.Add((grenade, 4f));
    }

    public void OnGrenadeExploded(Vector3 position)
    {
        var closest = _grenades.OrderBy(g => g.Body.GlobalPosition.DistanceSquaredTo(position)).FirstOrDefault();
        if (closest.Body is not null)
        {
            _grenades.Remove(closest);
            closest.Body.QueueFree();
        }
        Effects.Burst(_effects, position, new Color("ff8a3d"));
        Effects.Flash(_effects, position, new Color("ffb347"), 0.5f);
    }

    public void OnDuelEnded(int outcome)
    {
        if (_phase is not (Phase.Duel or Phase.DuelIntro)) return;
        var won = (DuelOutcome)outcome == DuelOutcome.AttackerWon == (_myRole == DuelRole.Attacker);
        _duelHud.ShowResult(won);
        SetTrigger(false);
        _phase = Phase.DuelOutro;
        if (_fighter is not null) _fighter.CanMove = false;
        After(1.3, EndDuel);
    }

    public void OnSaveData(string json)
    {
        try
        {
            _pause.ShowToast($"Gespeichert als \"{SaveStore.SaveJson(json)}\".");
        }
        catch (IOException e)
        {
            GD.PushError($"Saving failed: {e}");
            _pause.ShowToast($"Speichern fehlgeschlagen: {e.Message}");
        }
    }

    public void OnNotice(string text)
    {
        _hud.ShowToast(text);
        if (_pause.Visible) _pause.ShowToast(text);
    }

    public void OnClockSync(long whiteMs, long blackMs)
    {
        _whiteMs = whiteMs;
        _blackMs = blackMs;
    }

    // ================================================================ frame loop and input

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        TickClocks(delta);
        TickGrenades(dt);

        switch (_phase)
        {
            case Phase.Board:
                UpdateBoardAim();
                UpdateBoardHud();
                break;
            case Phase.DuelIntro:
            case Phase.Duel:
            case Phase.DuelOutro:
                UpdateDuel(dt);
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputSetup.Pause))
        {
            if (_pause.Visible) ClosePause();
            else if (_promotion.Visible) _promotion.Visible = false;
            else if (!_gameOver.Visible) OpenPause();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (OverlayOpen) return;

        if (_phase == Phase.Board)
        {
            if (_camera.HandleInput(@event)) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OnBoardClick();
            return;
        }

        if (_phase is Phase.Duel or Phase.DuelIntro) HandleDuelInput(@event);
    }

    private void OpenPause()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        // Only games against the computer can really pause, and never in the middle of a duel.
        if (VsComputer && _phase == Phase.Board) Channel.RpcId(1, MatchChannel.MethodName.SetPaused, true);
        SetTrigger(false);
        _pause.Open(canUndo: false, canResign: _game is { IsOver: false });
        _pause.SetSaveEnabled(VsComputer && _phase == Phase.Board);
        _pause.SetUndoVisible(false);
    }

    private void ClosePause()
    {
        _pause.Visible = false;
        if (VsComputer) Channel.RpcId(1, MatchChannel.MethodName.SetPaused, false);
        if (_phase is Phase.Duel or Phase.DuelIntro) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void TickClocks(double delta)
    {
        if (!_timed || _game is null || _game.IsOver || _game.PendingDuel is not null || _pause.Visible && VsComputer) return;
        if (_game.Position.SideToMove == PieceColor.White) _whiteMs = Math.Max(0, _whiteMs - delta * 1000);
        else _blackMs = Math.Max(0, _blackMs - delta * 1000);
    }

    // ================================================================ board phase

    private bool IsMyTurn => _game is { IsOver: false, PendingDuel: null } && _game.Position.SideToMove == _myColor;

    private void UpdateBoardAim()
    {
        Square? square = null;
        if (!OverlayOpen && _built)
        {
            var mouse = GetViewport().GetMousePosition();
            var from = _camera.ProjectRayOrigin(mouse);
            var to = from + _camera.ProjectRayNormal(mouse) * 300f;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(
                PhysicsRayQueryParameters3D.Create(from, to, BoardGeometry.WorldLayer | BoardGeometry.PieceLayer));
            if (hit.Count > 0)
            {
                square = hit["collider"].AsGodotObject() switch
                {
                    PieceActor actor => actor.Square,
                    StaticBody3D body when body == _table => BoardGeometry.SquareAt(hit["position"].AsVector3()),
                    _ => null,
                };
            }
        }

        if (square != _aimSquare)
        {
            _aimSquare = square;
            RefreshHighlights();
            Input.SetDefaultCursorShape(square is { } s && IsClickable(s) ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
        }
    }

    private bool IsClickable(Square square) =>
        IsMyTurn && (_targets.Any(m => m.To == square) || _game!.Position.PieceAt(square)?.Color == _myColor);

    private void OnBoardClick()
    {
        if (!IsMyTurn || _aimSquare is not { } square)
        {
            Deselect();
            return;
        }

        var moves = _targets.Where(m => m.To == square).ToList();
        if (moves.Count > 1)
        {
            _pendingPromotion = moves;
            _promotion.Open(_myColor);
            return;
        }
        if (moves.Count == 1)
        {
            Submit(moves[0]);
            return;
        }

        if (_game!.Position.PieceAt(square)?.Color == _myColor && square != _selected) Select(square);
        else Deselect();
    }

    private void Submit(Move move)
    {
        Deselect();
        Channel.RpcId(1, MatchChannel.MethodName.SubmitMove, move.ToUci());
    }

    private void Select(Square square)
    {
        Deselect();
        _selected = square;
        _targets = _game!.LegalMovesFrom(square).ToList();
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

    private void SyncPieces()
    {
        foreach (var actor in _actors.Values) actor.QueueFree();
        _actors.Clear();
        if (_game is null) return;
        foreach (var (square, piece) in _game.Position.Pieces())
        {
            var actor = PieceActor.Create(piece, square);
            _pieces.AddChild(actor);
            _actors[square] = actor;
        }
    }

    private void Animate(PlayedMove played)
    {
        if (!_actors.Remove(played.Move.From, out var mover))
        {
            SyncPieces();
            return;
        }

        if (played.Outcome == DuelOutcome.DefenderWon)
        {
            // The attacker lost its duel and disappears; the defender stays where it is.
            mover.Die();
            return;
        }

        var delay = 0f;
        if (played.CapturedOn is { } capturedOn && _actors.Remove(capturedOn, out var victim))
        {
            victim.Die();
            delay = 0.3f;
        }

        _actors[played.Move.To] = mover;
        var slide = mover.SlideTo(played.Move.To, hop: played.Mover.Type == PieceType.Knight, delay);
        if (played.Move.Promotion is { } promotion)
            slide.Finished += () => mover.SetPiece(new Piece(promotion, played.Mover.Color));

        if (played.RookMove is { } rook && _actors.Remove(rook.From, out var rookActor))
        {
            _actors[rook.To] = rookActor;
            rookActor.SlideTo(rook.To, hop: false, delay: 0.25f);
        }
    }

    private void UpdateBoardHud()
    {
        if (_game is null) return;
        string status;
        if (WaitingForOpponent)
            status = $"Warte auf einen Gegner ... Raumcode: {_state!.Code}" + (_lanHint is null ? "" : $"\n{_lanHint}");
        else if (_game.IsOver) status = Texts.Result(_game);
        else if (_game.PendingDuel is not null) status = "Duell!";
        else if (IsMyTurn) status = "Du bist am Zug." + (_game.Position.IsInCheck(_myColor) ? " Schach!" : "");
        else status = _state?.VsComputer == true ? "Der Computer denkt nach ..." : "Der Gegner ist am Zug.";
        _hud.SetStatus(status);
        if (_timed) _hud.SetPlayers(PlayersText());
    }

    private string PlayersText()
    {
        if (_state is null) return "";
        string Line(PieceColor color, PlayerInfo player, double ms)
        {
            var marker = _game is { IsOver: false } && _game.Position.SideToMove == color ? "▶ " : "   ";
            var you = color == _myColor ? " (du)" : "";
            var clock = _timed ? $"   {Texts.Clock(TimeSpan.FromMilliseconds(ms))}" : "";
            return $"{marker}{Texts.Color(color)}: {player.Name}{you}{clock}";
        }
        return $"{Line(PieceColor.White, _state.White, _whiteMs)}\n{Line(PieceColor.Black, _state.Black, _blackMs)}";
    }

    private void RefreshMoveList()
    {
        if (_game is null) return;
        var lines = Texts.MoveListLines(_game).ToList();
        _hud.SetMoves(lines.Count == 0 ? "noch keine" : string.Join("\n", lines.TakeLast(10)));
    }

    private void ShowGameOver()
    {
        Deselect();
        After(1.2, () =>
        {
            if (_game is not { IsOver: true } || _phase != Phase.Board) return;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _pause.Visible = false;
            _gameOver.Open(Texts.Result(_game));
        });
    }

    // ================================================================ highlights

    private void BuildOverlays()
    {
        var mesh = new PlaneMesh { Size = new Vector2(BoardGeometry.Tile * 0.96f, BoardGeometry.Tile * 0.96f) };
        for (var index = 0; index < 64; index++)
        {
            _overlays[index] = new MeshInstance3D
            {
                Mesh = mesh,
                Position = BoardGeometry.Center(Square.FromIndex(index)) + new Vector3(0, 0.04f, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            _world.AddChild(_overlays[index]);
        }
    }

    private void RefreshHighlights()
    {
        if (_game is null || _overlays[0] is null) return;
        var position = _game.Position;
        Square? checkedKing = position.IsInCheck(position.SideToMove) ? position.TryKingSquare(position.SideToMove) : null;
        var last = _game.LastMove;

        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            var aimed = square == _aimSquare;
            var isTarget = _targets.Any(m => m.To == square);

            var highlight = Highlight.None;
            if (last is { } l && (l.From == square || l.To == square)) highlight = Highlight.LastMove;
            if (aimed && IsMyTurn && position.PieceAt(square)?.Color == _myColor) highlight = Highlight.Hover;
            if (square == checkedKing) highlight = Highlight.Check;
            if (square == _selected) highlight = Highlight.Selected;
            if (isTarget)
            {
                var capture = position.IsCapture(_targets.First(m => m.To == square));
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
            Highlight.Hover => new Color(1f, 1f, 1f, 0.25f),
            Highlight.Selected => new Color(1f, 0.78f, 0.25f, 0.6f),
            Highlight.Move => new Color(0.25f, 0.95f, 0.45f, 0.45f),
            Highlight.MoveHover => new Color(0.45f, 1f, 0.6f, 0.8f),
            Highlight.Capture => new Color(1f, 0.25f, 0.2f, 0.55f),
            _ => new Color(1f, 0.45f, 0.35f, 0.9f),
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

    // ================================================================ duel phase

    // Timeline of the intro, in seconds after the capture; the server's NetConfig.DuelIntroSeconds covers all of it.
    private const double VersusCardAt = 1.3;
    private const double FlyInAt = 3.0;
    private const float FlyInSeconds = 1.05f;

    /// <summary>
    /// Shows who attacks whom before the duel: an arrow and a lunge on the board with a banner, then a
    /// versus card with both fighters, then the camera flies into the player's own piece.
    /// </summary>
    private void StartDuelIntro(Square attackerSquare, Square defenderSquare, PieceType attackerType, PieceType defenderType)
    {
        _phase = Phase.DuelIntro;
        var duelId = ++_duelCounter;
        _gameOver.Visible = false;
        Deselect();
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);

        var attackerColor = Enum.Parse<PieceColor>(_duelInfo!.AttackerColor);
        var attacker = new Piece(attackerType, attackerColor);
        var defender = new Piece(defenderType, attackerColor.Opponent());
        var mine = _myRole == DuelRole.Attacker ? attackerSquare : defenderSquare;
        var theirs = _myRole == DuelRole.Attacker ? defenderSquare : attackerSquare;
        var myPiece = _myRole == DuelRole.Attacker ? attacker : defender;
        var theirPiece = _myRole == DuelRole.Attacker ? defender : attacker;
        var opponentOwner = theirPiece.Color == PieceColor.White ? _state!.White.Name : _state!.Black.Name;

        // Stage 1: on the board, who attacks whom.
        var attackerAt = BoardGeometry.Center(attackerSquare);
        var defenderAt = BoardGeometry.Center(defenderSquare);
        foreach (var overlay in _overlays) overlay.Visible = false;
        ShowOverlay(attackerSquare, Highlight.Selected);
        ShowOverlay(defenderSquare, Highlight.Capture);
        _attackArrow = AttackArrow(attackerAt, defenderAt, attackerColor == PieceColor.White ? WhiteShot : BlackShot);
        _world.AddChild(_attackArrow);
        if (_actors.TryGetValue(attackerSquare, out var lunging)) Lunge(lunging, defenderAt);
        _hud.ShowBanner(Texts.AttackAnnouncement(attacker, defender, defenderSquare, _myColor), new Color("ff6a4f"));

        // Stage 2: introduce both fighters.
        After(VersusCardAt, () =>
        {
            if (duelId != _duelCounter || _phase != Phase.DuelIntro) return;
            _versus.Open(FighterClasses.For(myPiece.Type), FighterClasses.For(theirPiece.Type), opponentOwner,
                _myRole == DuelRole.Attacker);
        });

        // Stage 3: into the fighter's eyes.
        After(FlyInAt, () =>
        {
            if (duelId != _duelCounter || _phase != Phase.DuelIntro) return;
            _versus.Visible = false;
            _hud.Visible = false;
            _attackArrow?.QueueFree();
            _attackArrow = null;
            foreach (var overlay in _overlays) overlay.Visible = false;
            SpawnFighters(mine, theirs, myPiece, theirPiece, opponentOwner);

            _camera.Orbiting = false;
            var tween = CreateTween();
            tween.TweenProperty(_camera, "global_transform", _fighter!.Camera.GlobalTransform, FlyInSeconds)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenCallback(Callable.From(() =>
            {
                if (_fighter is null || duelId != _duelCounter) return;
                _fighter.Camera.Current = true;
                _duelHud.Visible = true;
                if (!OverlayOpen) Input.MouseMode = Input.MouseModeEnum.Captured;
                if (_phase == Phase.DuelIntro) _phase = Phase.Duel;
            }));
        });
    }

    private void SpawnFighters(Square mine, Square theirs, Piece myPiece, Piece theirPiece, string opponentOwner)
    {
        foreach (var (square, actor) in _actors)
        {
            if (square == mine || square == theirs)
            {
                actor.Visible = false;
                actor.CollisionLayer = 0;
                _hiddenActors.Add(actor);
            }
            else
            {
                actor.SetDimmed(true);
            }
        }

        var myAt = BoardGeometry.Center(mine);
        var theirAt = BoardGeometry.Center(theirs);
        _opponent = new RemoteFighter();
        _opponent.Init(theirPiece, theirAt, BoardGeometry.YawTowards(theirAt, myAt), opponentOwner);
        _world.AddChild(_opponent);
        var theirHealth = _myRole == DuelRole.Attacker ? _serverDefenderHealth : _serverAttackerHealth;
        _opponent.SetHealth(theirHealth, FighterClasses.For(theirPiece.Type).MaxHealth);

        _fighter = new LocalFighter { Position = myAt, Rotation = new Vector3(0, BoardGeometry.YawTowards(myAt, theirAt), 0) };
        _fighter.Init(myPiece.Type, _myColor);
        _world.AddChild(_fighter);

        _duelHud.Setup(FighterClasses.For(myPiece.Type), FighterClasses.For(theirPiece.Type), opponentOwner);
    }

    private void ShowOverlay(Square square, Highlight highlight)
    {
        var overlay = _overlays[square.Index];
        overlay.Visible = true;
        overlay.MaterialOverride = HighlightMaterial(highlight);
    }

    /// <summary>A glowing arrow along the board from the attacker to its target.</summary>
    private static Node3D AttackArrow(Vector3 from, Vector3 to, Color color)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 2f,
        };
        var root = new Node3D { Position = from + Vector3.Up * 0.6f, Rotation = new Vector3(0, BoardGeometry.YawTowards(from, to), 0) };
        var length = from.DistanceTo(to) - 1.4f;
        // Built along -Z (the forward direction of the yaw).
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = length, RadialSegments = 8 },
            Position = new Vector3(0, 0, -length / 2f - 0.3f),
            RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.45f, Height = 0.9f, RadialSegments = 12 },
            Position = new Vector3(0, 0, -length - 0.7f),
            RotationDegrees = new Vector3(-90, 0, 0),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        var pulse = root.CreateTween().SetLoops();
        pulse.TweenProperty(material, "emission_energy_multiplier", 4f, 0.35f);
        pulse.TweenProperty(material, "emission_energy_multiplier", 1.5f, 0.35f);
        return root;
    }

    /// <summary>The attacker jumps a little towards its target and back.</summary>
    private void Lunge(PieceActor actor, Vector3 target)
    {
        var home = actor.Position;
        var toward = home.Lerp(target, 0.35f) + Vector3.Up * 0.8f;
        var tween = actor.CreateTween();
        tween.TweenProperty(actor, "position", toward, 0.35f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(actor, "position", home, 0.45f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    private void HandleDuelInput(InputEvent @event)
    {
        if (_fighter is null || _localDuel is null) return;
        if (!_fighter.ControlsEnabled)
        {
            if (@event is InputEventMouseButton { Pressed: true }) Input.MouseMode = Input.MouseModeEnum.Captured;
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                SetTrigger(button.Pressed);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } button:
                _zoomed = button.Pressed;
                _fighter.SetZoom(_zoomed);
                _duelHud.SetZoomed(_zoomed && _fighter.Class.ZoomFov is not null);
                _fighter.Weapon.Visible = !(_zoomed && _fighter.Class.ZoomFov is not null);
                break;
            case InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.R }:
                if (_localDuel.StartReload(_myRole)) Channel.RpcId(1, MatchChannel.MethodName.Reload);
                break;
            case InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.G }:
                ThrowGrenade();
                break;
            case InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Shift } when _fighter.Class.Dash is { } dash:
                if (_localDuel.TryDash(_myRole))
                {
                    _fighter.Dash(dash.Distance);
                    Channel.RpcId(1, MatchChannel.MethodName.Dash);
                }
                break;
        }
    }

    private void SetTrigger(bool held)
    {
        if (_triggerHeld == held) return;
        _triggerHeld = held;
        if (_localDuel is null || _phase is not (Phase.Duel or Phase.DuelIntro)) return;
        _localDuel.SetTrigger(_myRole, held);
        if (_localDuel[_myRole].Class.Weapon.SpinUpSeconds > 0) Channel.RpcId(1, MatchChannel.MethodName.Trigger, held);
    }

    private void UpdateDuel(float dt)
    {
        if (_fighter is null || _localDuel is null) return;

        // Our weapon timers start with the server's: after the intro the shared countdown runs.
        if (_serverIntro <= 0f) _localDuel.Tick(dt);
        _fighter.CanMove = _phase == Phase.Duel && _localDuel.Phase == DuelPhase.Fighting;

        var me = _localDuel[_myRole];
        var ownHealth = _myRole == DuelRole.Attacker ? _serverAttackerHealth : _serverDefenderHealth;
        var enemyHealth = _myRole == DuelRole.Attacker ? _serverDefenderHealth : _serverAttackerHealth;
        _duelHud.Refresh(me, ownHealth, enemyHealth, _serverTimeLeft, _serverCountdown, _serverIntro, _localDuel.Phase == DuelPhase.Fighting);
        if (me.Class.Weapon.SpinUpSeconds > 0) _fighter.Weapon.SetSpin(me.SpinUp / me.Class.Weapon.SpinUpSeconds);

        if (_phase == Phase.Duel && _triggerHeld && _fighter.ControlsEnabled && !OverlayOpen) TryShoot();

        _sinceInput += dt;
        if (_phase != Phase.DuelOutro && _sinceInput >= NetConfig.SyncInterval)
        {
            _sinceInput = 0f;
            Channel.RpcId(1, MatchChannel.MethodName.DuelInput, _fighter.GlobalPosition, _fighter.Rotation.Y, _fighter.Pitch);
        }
    }

    private void TryShoot()
    {
        if (_fighter is null || _localDuel is null || !_localDuel.TryFire(_myRole)) return;

        var weapon = _fighter.Class.Weapon;
        var origin = _fighter.Camera.GlobalPosition;
        var directions = Ballistics.ShotDirections(_fighter.AimDirection, weapon, _zoomed);
        var space = GetWorld3D().DirectSpaceState;
        var color = _myColor == PieceColor.White ? WhiteShot : BlackShot;
        var ends = new Vector3[directions.Length];
        var hitMask = 0;
        for (var i = 0; i < directions.Length; i++)
        {
            // Hits are judged against what this player sees; the server double-checks them.
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, origin + directions[i] * weapon.MaxRange,
                BoardGeometry.WorldLayer | BoardGeometry.PieceLayer, [_fighter.GetRid()]));
            ends[i] = hit.Count > 0 ? hit["position"].AsVector3() : origin + directions[i] * weapon.MaxRange;
            if (hit.Count > 0 && hit["collider"].AsGodotObject() == _opponent) hitMask |= 1 << i;
            Effects.Tracer(_effects, _fighter.Weapon.MuzzlePosition, ends[i], color, flash: i == 0);
        }
        if (hitMask != 0) _duelHud.HitMarker();
        _fighter.Weapon.Recoil();
        Channel.RpcId(1, MatchChannel.MethodName.Fire, origin, ends, hitMask);
    }

    private void ThrowGrenade()
    {
        if (_fighter is null || _localDuel is null || _fighter.Class.Grenades is null || _phase != Phase.Duel) return;
        if (!_localDuel.TryThrowGrenade(_myRole)) return;
        var origin = _fighter.Camera.GlobalPosition + _fighter.AimDirection * 0.8f;
        var velocity = _fighter.AimDirection * 14f + Vector3.Up * 4f;
        Channel.RpcId(1, MatchChannel.MethodName.ThrowGrenade, origin, velocity);
    }

    private void TickGrenades(float dt)
    {
        for (var i = _grenades.Count - 1; i >= 0; i--)
        {
            var (body, life) = _grenades[i];
            life -= dt;
            if (life > 0f)
            {
                _grenades[i] = (body, life);
                continue;
            }
            _grenades.RemoveAt(i);
            body.QueueFree();
        }
    }

    /// <summary>Leaves the duel view; also called when a game ends while the intro is still running.</summary>
    private void EndDuel()
    {
        if (_phase == Phase.Board) return;
        _duelCounter++;

        // Fly back up from wherever the fighter ended (or straight from the board if it never spawned).
        if (_fighter is not null) _camera.GlobalTransform = _fighter.Camera.GlobalTransform;
        _camera.Current = true;
        _duelHud.Visible = false;
        _duelHud.SetZoomed(false);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _fighter?.QueueFree();
        _fighter = null;
        _opponent?.QueueFree();
        _opponent = null;
        _localDuel = null;
        _versus.Visible = false;
        _attackArrow?.QueueFree();
        _attackArrow = null;
        foreach (var actor in _actors.Values) actor.SetDimmed(false);
        foreach (var grenade in _grenades) grenade.Body.QueueFree();
        _grenades.Clear();

        var tween = CreateTween();
        tween.TweenProperty(_camera, "global_transform", _camera.OrbitTransform, 0.9f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() =>
        {
            _camera.Orbiting = true;
            foreach (var actor in _hiddenActors)
            {
                if (!IsInstanceValid(actor)) continue;
                actor.Visible = true;
                actor.CollisionLayer = BoardGeometry.PieceLayer;
            }
            _hiddenActors.Clear();
            _hud.Visible = true;
            _phase = Phase.Board;
            while (_afterDuel.Count > 0 && _phase == Phase.Board) _afterDuel.Dequeue()();
            RefreshHighlights();
        }));
    }

    /// <summary>Delayed action on a tween owned by this screen, so it never outlives it.</summary>
    private void After(double seconds, Action action)
    {
        var tween = CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenCallback(Callable.From(action));
    }
}
