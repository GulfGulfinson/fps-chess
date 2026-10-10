using Chess.Core;
using Chess.Core.Duels;
using Chess.Core.Persistence;
using Chess.Core.Session;
using FpsChess.Arena;
using Godot;

namespace FpsChess.Net.Server;

/// <summary>
/// One game on the server: seats, the authoritative <see cref="GameSession"/>, duels and the room's own
/// physics world. Everything players do arrives here through <see cref="MatchChannel"/> and is checked.
/// </summary>
public partial class MatchRoom : Node, IMatchServer
{
    private const double ClockSyncSeconds = 0.5;

    private readonly Dictionary<PieceColor, int> _seats = [];
    private readonly Dictionary<PieceColor, string> _names = [];
    private readonly Dictionary<PieceColor, string> _tokens = [];

    // Seconds since a seated player lost the connection; their seat is kept for a while.
    private readonly Dictionary<PieceColor, double> _absentFor = [];
    private string? _currentDuelJson;
    private LobbyServer _lobby = null!;
    private MatchChannel _channel = null!;
    private RoomSettings _settings = null!;
    private GameSetup _setup = null!;
    private SavedGame? _save;
    private GameSession? _session;
    private ServerDuel? _duel;
    private Node3D _world = null!;
    private double _sinceClockSync;

    public string Code { get; private set; } = "";
    public ArenaThemeId Theme { get; private set; }

    public bool HasFreeSeat => _session is null && HumanColors.Any(c => !_seats.ContainsKey(c));
    public bool IsListed => _settings.Public && !_settings.VsComputer && HasFreeSeat;

    private IEnumerable<PieceColor> HumanColors =>
        new[] { PieceColor.White, PieceColor.Black }.Where(c => _setup.PlayerFor(c) == PlayerKind.Human);

    private IEnumerable<int> Peers => _seats.Values.Where(_lobby.IsConnected);

    /// <returns>An error message for the creator, or null when the room is ready for players.</returns>
    public string? Init(string code, RoomSettings settings, MatchChannel channel, LobbyServer lobby)
    {
        Code = code;
        Name = "Room";
        _settings = settings;
        _channel = channel;
        _lobby = lobby;
        channel.Server = this;
        Theme = ArenaTheme.Random();

        if (settings.SaveJson is { } json)
        {
            if (!settings.VsComputer) return "Spielstände können nur gegen den Computer fortgesetzt werden.";
            try
            {
                _save = GameSerializer.Deserialize(json);
            }
            catch (InvalidSaveFileException e)
            {
                return $"Der Spielstand ist beschädigt: {e.Message}";
            }
            if (_save.Setup.Variant != GameVariant.Duel) return "Dieser Spielstand gehört zum 2D-Modus.";
            _setup = _save.Setup;
        }
        else
        {
            var humanColor = settings.HumanColor switch
            {
                "Black" => PieceColor.Black,
                "Random" => Random.Shared.Next(2) == 0 ? PieceColor.White : PieceColor.Black,
                _ => PieceColor.White,
            };
            var opponent = settings.VsComputer ? PlayerKind.Computer : PlayerKind.Human;
            TimeControl? timeControl = settings is { InitialMs: > 0 and { } initial, IncrementMs: { } increment }
                ? new TimeControl(TimeSpan.FromMilliseconds(initial), TimeSpan.FromMilliseconds(increment))
                : null;
            var aiLevel = Math.Clamp(settings.AiLevel, GameSetup.MinAiLevel, GameSetup.MaxAiLevel);
            _setup = humanColor == PieceColor.White
                ? new GameSetup(PlayerKind.Human, opponent, aiLevel, timeControl, GameVariant.Duel)
                : new GameSetup(opponent, PlayerKind.Human, aiLevel, timeControl, GameVariant.Duel);
            // The creator's colour is the first free human seat; for online games put them on the chosen side.
            _preferredColor = humanColor;
        }

        foreach (var color in new[] { PieceColor.White, PieceColor.Black })
        {
            if (_setup.PlayerFor(color) == PlayerKind.Computer) _names[color] = $"Computer (Stufe {_setup.AiLevel})";
        }

        var viewport = new SubViewport { OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Size = new Vector2I(2, 2) };
        AddChild(viewport);
        _world = new Node3D { Name = "World" };
        viewport.AddChild(_world);
        ArenaBuilder.Build(_world, Theme, visuals: false);
        return null;
    }

    private PieceColor? _preferredColor;

    public PieceColor Seat(int peer, string name)
    {
        var free = HumanColors.Where(c => !_seats.ContainsKey(c)).ToList();
        var color = _preferredColor is { } preferred && free.Contains(preferred) ? preferred : free[0];
        _seats[color] = peer;
        _names[color] = name;
        _tokens[color] = Guid.NewGuid().ToString("N");
        return color;
    }

    public string TokenFor(PieceColor color) => _tokens[color];

    /// <returns>The seat the token belongs to, if its player is currently away.</returns>
    public PieceColor? TryReclaim(int peer, string token)
    {
        foreach (var (color, seatToken) in _tokens)
        {
            if (seatToken == token && _absentFor.ContainsKey(color)) return color;
        }
        return null;
    }

    public void OnReclaimed(int peer, PieceColor color)
    {
        _seats[color] = peer;
        _absentFor.Remove(color);
        _duel?.ReassignPeer(color, peer);
        SendState(peer);
        if (_currentDuelJson is { } duel) _channel.RpcId(peer, MatchChannel.MethodName.DuelStarted, duel);
        foreach (var other in Peers.Where(p => p != peer))
            _channel.RpcId(other, MatchChannel.MethodName.Notice, $"{_names[color]} ist wieder verbunden.");
    }

    public void OnSeated(int peer)
    {
        if (HumanColors.All(_seats.ContainsKey)) StartGame();
        else SendState(peer);
    }

    public RoomListEntry ListEntry() => new()
    {
        Code = Code,
        Host = _names.Values.FirstOrDefault() ?? "",
        TimeControl = _setup.TimeControl is { } tc ? $"{tc.Initial.TotalMinutes:0} + {tc.Increment.TotalSeconds:0}" : "ohne Uhr",
    };

    private void StartGame()
    {
        _session = _save is { } save
            ? new GameSession(save.Setup, save.Game, save.Clock)
            : new GameSession(_setup);
        _session.MovePlayed += OnMovePlayed;
        _session.DuelStarted += OnDuelStarted;
        _session.GameEnded += OnGameEnded;
        foreach (var peer in Peers) SendState(peer);
    }

    public override void _Process(double delta)
    {
        TickAbsentPlayers(delta);
        if (_session is null) return;
        try
        {
            _session.Update(TimeSpan.FromSeconds(delta));
        }
        catch (Exception e)
        {
            Abort(e);
            return;
        }

        if (_session.Clock is { } clock && (_sinceClockSync += delta) >= ClockSyncSeconds)
        {
            _sinceClockSync = 0;
            SendAll(MatchChannel.MethodName.ClockSync,
                (long)clock.Remaining(PieceColor.White).TotalMilliseconds, (long)clock.Remaining(PieceColor.Black).TotalMilliseconds);
        }
    }

    public override void _ExitTree() => _session?.Dispose();

    // ---------------------------------------------------------------- session events

    private void OnMovePlayed(PlayedMove played) =>
        SendAll(MatchChannel.MethodName.MovePlayed, played.Move.ToUci(), played.Outcome is { } o ? (int)o : -1);

    private void OnDuelStarted(PendingDuel pending)
    {
        var dto = new DuelStartDto
        {
            Move = pending.Move.ToUci(),
            AttackerSquare = pending.AttackerSquare.ToString(),
            DefenderSquare = pending.DefenderSquare.ToString(),
            Attacker = pending.Attacker.Type.ToString(),
            Defender = pending.Defender.Type.ToString(),
            AttackerColor = pending.Attacker.Color.ToString(),
        };

        // 0 means the computer plays this side; an absent human gets -1 so no bot takes over their piece.
        int PeerOf(PieceColor color) => _setup.PlayerFor(color) == PlayerKind.Computer ? 0
            : _seats.TryGetValue(color, out var p) && p != 0 ? p : -1;

        _duel = new ServerDuel();
        AddChild(_duel);
        _duel.Init(this, _world, pending, _session!.Game.Position, _setup.AiLevel,
            PeerOf(pending.Attacker.Color), PeerOf(pending.Defender.Color));
        _currentDuelJson = NetJson.Write(dto);
        SendAll(MatchChannel.MethodName.DuelStarted, _currentDuelJson);
    }

    /// <summary>Called by the duel once its outcome has been shown for a moment.</summary>
    public void FinishDuel(DuelOutcome outcome)
    {
        _duel?.QueueFree();
        _duel = null;
        _currentDuelJson = null;
        SendAll(MatchChannel.MethodName.DuelEnded, (int)outcome);
        _session?.ResolveDuel(outcome);
    }

    private void OnGameEnded()
    {
        _duel?.QueueFree();
        _duel = null;
        _currentDuelJson = null;
        foreach (var peer in Peers) SendState(peer);
    }

    // ---------------------------------------------------------------- player requests

    private PieceColor? ColorOf(int peer) =>
        _seats.FirstOrDefault(s => s.Value == peer) is { Value: not 0 } seat ? seat.Key : null;

    public void OnSubmitMove(int peer, string uci)
    {
        if (_session is null || ColorOf(peer) is not { } color) return;
        Move move;
        try
        {
            move = Move.ParseUci(uci);
        }
        catch (FormatException)
        {
            GD.PushWarning($"[room {Code}] peer {peer} sent malformed move '{uci}'");
            return;
        }
        if (color != _session.Turn || !_session.TryPlayHumanMove(move))
        {
            GD.PushWarning($"[room {Code}] rejected move {uci} from {color}");
            _channel.RpcId(peer, MatchChannel.MethodName.Notice, "Dieser Zug ist nicht erlaubt.");
            SendState(peer);
        }
    }

    public void OnResign(int peer)
    {
        if (_session is not null && ColorOf(peer) is { } color) _session.Resign(color);
    }

    public void OnSaveRequested(int peer)
    {
        if (_session is null || ColorOf(peer) is null) return;
        if (!_settings.VsComputer)
        {
            _channel.RpcId(peer, MatchChannel.MethodName.Notice, "Online-Partien werden nicht gespeichert.");
            return;
        }
        if (_session.Game.PendingDuel is not null)
        {
            _channel.RpcId(peer, MatchChannel.MethodName.Notice, "Während eines Duells kann nicht gespeichert werden.");
            return;
        }
        _channel.RpcId(peer, MatchChannel.MethodName.SaveData, GameSerializer.Serialize(_session.ToSave()));
    }

    public void OnPauseRequested(int peer, bool paused)
    {
        if (_session is not null && _settings.VsComputer && ColorOf(peer) is not null) _session.Paused = paused;
    }

    public void OnDuelInput(int peer, Vector3 position, float yaw, float pitch) => _duel?.OnInput(peer, position, yaw, pitch);
    public void OnTrigger(int peer, bool held) => _duel?.OnTrigger(peer, held);
    public void OnFire(int peer, Vector3 origin, Vector3[] ends, int hitMask) => _duel?.OnFire(peer, origin, ends, hitMask);
    public void OnReload(int peer) => _duel?.OnReload(peer);
    public void OnDash(int peer) => _duel?.OnDash(peer);
    public void OnThrowGrenade(int peer, Vector3 origin, Vector3 velocity) => _duel?.OnThrowGrenade(peer, origin, velocity);

    public void OnPeerLeft(int peer)
    {
        if (ColorOf(peer) is not { } color) return;
        GD.Print($"[room {Code}] {color} left");

        // A game against the computer or a room still waiting for its opponent has nothing to keep.
        if (_settings.VsComputer || _session is null || _session.Game.IsOver)
        {
            _seats.Remove(color);
            if (!Peers.Any()) _lobby.CloseRoom(this);
            return;
        }

        _seats[color] = 0;
        _absentFor[color] = 0;
        SendAll(MatchChannel.MethodName.Notice,
            $"{_names[color]} hat die Verbindung verloren und hat {NetConfig.ReconnectSeconds:0} Sekunden, um zurückzukommen.");
    }

    private void TickAbsentPlayers(double delta)
    {
        foreach (var color in _absentFor.Keys.ToList())
        {
            _absentFor[color] += delta;
            if (_absentFor[color] < NetConfig.ReconnectSeconds) continue;

            _absentFor.Remove(color);
            _seats.Remove(color);
            if (_session is { Game.IsOver: false })
            {
                _session.Resign(color);
                SendAll(MatchChannel.MethodName.Notice, $"{_names[color]} ist nicht zurückgekommen.");
            }
            if (!Peers.Any())
            {
                _lobby.CloseRoom(this);
                return;
            }
        }
    }

    // ---------------------------------------------------------------- sending

    public void SendAll(StringName method, params Variant[] args)
    {
        foreach (var peer in Peers) _channel.RpcId(peer, method, args);
    }

    private void SendState(int peer) => _channel.RpcId(peer, MatchChannel.MethodName.State, NetJson.Write(BuildState()));

    private MatchStateDto BuildState()
    {
        var game = _session?.Game ?? _save?.Game;
        var clock = _session?.Clock ?? _save?.Clock;
        return new MatchStateDto
        {
            Code = Code,
            Theme = Theme.ToString(),
            White = Player(PieceColor.White),
            Black = Player(PieceColor.Black),
            VsComputer = _settings.VsComputer,
            AiLevel = _setup.AiLevel,
            StartFen = game?.StartFen ?? Position.StartFen,
            Moves = game is null ? [] : GameSerializer.EncodeMoves(game).ToList(),
            PendingMove = game?.PendingDuel?.Move.ToUci(),
            InitialMs = (long?)_setup.TimeControl?.Initial.TotalMilliseconds,
            IncrementMs = (long?)_setup.TimeControl?.Increment.TotalMilliseconds,
            WhiteMs = (long?)clock?.Remaining(PieceColor.White).TotalMilliseconds,
            BlackMs = (long?)clock?.Remaining(PieceColor.Black).TotalMilliseconds,
            ForfeitStatus = game?.ForfeitedBy is null ? null : game.Status.ToString(),
            ForfeitedBy = game?.ForfeitedBy?.ToString(),
        };
    }

    private PlayerInfo Player(PieceColor color) => new()
    {
        Name = _names.GetValueOrDefault(color, "(wartet)"),
        IsComputer = _setup.PlayerFor(color) == PlayerKind.Computer,
        Connected = _setup.PlayerFor(color) == PlayerKind.Computer || (_seats.TryGetValue(color, out var p) && _lobby.IsConnected(p)),
    };

    /// <summary>A bug inside this room ends only this room, never the whole server.</summary>
    public void Abort(Exception e)
    {
        GD.PushError($"[room {Code}] crashed: {e}");
        SendAll(MatchChannel.MethodName.Notice, "Serverfehler: Die Partie wurde abgebrochen.");
        _session?.Dispose();
        _session = null;
        _lobby.CloseRoom(this);
    }
}
