using Chess.Core;
using Chess.Core.Duels;
using Chess.Core.Persistence;
using FpsChess.Arena;
using FpsChess.Duel;
using FpsChess.Net;
using FpsChess.Net.Client;
using FpsChess.Net.Server;
using Godot;

namespace FpsChess.Tools;

/// <summary>
/// Headless integration test of the match server over real WebSockets.
///   godot --headless --path game res://tools/ServerTest.tscn                     (starts its own server)
///   godot --headless --path game res://tools/ServerTest.tscn -- --url wss://...  (tests a deployed server)
///   add --idle 130 to also check that a silent connection survives proxies' idle timeouts
/// Exits with code 0 when every scenario passed, 1 otherwise.
/// </summary>
public partial class ServerTest : Node
{
    private double _timeoutSeconds = 90;

    private readonly List<string> _failures = [];
    private int _port;
    private string _url = "";
    private double _elapsed;
    private Func<double, bool>? _step;
    private readonly Queue<(string Name, Func<double, bool> Step)> _steps = new();
    private string _currentStep = "";

    private TestPlayer _a = null!;
    private TestPlayer _b = null!;
    private TestPlayer _c = null!;
    private TestPlayer? _bBack;
    private TestPlayer? _intruder;
    private string _roomCode = "";
    private double _sinceShot;
    private bool _cheated;
    private bool _headshotFired;
    private bool _rematchAsked;
    private bool _chatted;
    private double _chatWait;
    private bool _rematchAccepted;
    private double _waited;
    private double _moveTime;
    private readonly List<(double Time, Vector3 Position)> _bTrail = [];

    private static Vector3 EyeOf(Vector3 feet) => feet + Vector3.Up * Ballistics.EyeHeight(PieceModels.Height(PieceType.Pawn));

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var urlIndex = Array.IndexOf(args, "--url");
        if (urlIndex >= 0 && urlIndex + 1 < args.Length)
        {
            _url = args[urlIndex + 1];
            GD.Print($"[test] against {_url}");
        }
        else
        {
            _port = Random.Shared.Next(30000, 60000);
            _url = $"ws://127.0.0.1:{_port}";
            LobbyServer.Start(this, _port, "127.0.0.1", out var error);
            if (error != Error.Ok)
            {
                Fail($"server did not start: {error}");
                Finish();
                return;
            }
        }

        var idleIndex = Array.IndexOf(args, "--idle");
        if (idleIndex >= 0 && idleIndex + 1 < args.Length && double.TryParse(args[idleIndex + 1], out var idleSeconds))
        {
            _timeoutSeconds += idleSeconds;
            TestPlayer? idler = null;
            var idleFor = 0.0;
            Then($"a silent connection survives {idleSeconds:0} s", delta =>
            {
                idler ??= TestPlayer.Connect(this, _url, "Idle");
                if (!idler.Welcomed) return false;
                if (idler.Errors.Count > 0 || idler.Disconnected)
                {
                    Fail($"the idle connection dropped after {idleFor:0} s");
                    return true;
                }
                return (idleFor += delta) >= idleSeconds;
            });
        }

        // ---- scenario 1: two humans online
        Then("A and B connect", _ =>
        {
            _a ??= TestPlayer.Connect(this, _url, "Alice");
            _b ??= TestPlayer.Connect(this, _url, "Bob");
            return _a.Welcomed && _b.Welcomed;
        });
        Then("A creates a public room", _ =>
        {
            if (!_a.CreatedRequest)
            {
                _a.CreatedRequest = true;
                _a.Client.CreateRoom(new RoomSettings { PlayerName = "Alice", HumanColor = "White", Public = true });
            }
            return _a.Color is not null;
        });
        Then("B finds the room in the list and joins", _ =>
        {
            if (_b.Rooms is null)
            {
                if (!_b.ListRequested)
                {
                    _b.ListRequested = true;
                    _b.Client.RequestRoomList();
                }
                return false;
            }
            if (_b.Color is null && !_b.JoinRequested)
            {
                // A deployed server may list other people's rooms; ours is the one Alice hosts.
                var ours = _b.Rooms.FirstOrDefault(r => r.Host == "Alice");
                Check(ours is not null, $"Alice's room is not listed ({_b.Rooms.Count} rooms)");
                _roomCode = ours?.Code ?? "";
                _b.JoinRequested = true;
                _b.Client.JoinRoom(_roomCode);
            }
            return _b.Color is not null && _a.States > 0 && _b.States > 0 && _a.Game is not null && _b.Game is not null;
        });
        Then("colours are assigned", _ =>
        {
            Check(_a.Color == PieceColor.White && _b.Color == PieceColor.Black, $"colours were {_a.Color}/{_b.Color}");
            return true;
        });
        Then("moves reach both players", _ =>
        {
            if (_a.Moves.Count == 0 && !_a.Sent.Contains("e2e4")) _a.Submit("e2e4");
            if (_a.Moves.Count == 1 && !_b.Sent.Contains("d7d5")) _b.Submit("d7d5");
            return _a.Moves.Count == 2 && _b.Moves.Count == 2;
        });
        Then("a move out of turn is rejected", _ =>
        {
            if (!_b.Sent.Contains("e7e5"))
            {
                _b.Submit("e7e5");
                return false;
            }
            return _b.Notices.Count > 0;
        });
        Then("a quick message reaches the opponent and spamming is limited", _ =>
        {
            if (!_chatted)
            {
                _chatted = true;
                _a.Channel.RpcId(1, MatchChannel.MethodName.SendChat, 1);
                _a.Channel.RpcId(1, MatchChannel.MethodName.SendChat, 2);
                _a.Channel.RpcId(1, MatchChannel.MethodName.SendChat, 99);
            }
            if ((_chatWait += 1.0 / 60.0) < 0.5) return false;
            Check(_b.Chats.Count == 1 && _b.Chats[0] == ("Alice", 1), $"B got {string.Join(", ", _b.Chats)}");
            Check(_a.Chats.Count == 0, "the sender got its own message back");
            return true;
        });
        Then("a capture starts a duel for both", _ =>
        {
            if (!_a.Sent.Contains("e4d5")) _a.Submit("e4d5");
            return _a.Duel is not null && _b.Duel is not null;
        });
        Then("the duel reaches the fighting phase", delta =>
        {
            _a.SendPose(BoardGeometry.Center(Square.Parse("e4")));
            _b.SendPose(BoardGeometry.Center(Square.Parse("d5")));
            return _a.Fighting && _b.Fighting;
        });
        Then("a claimed hit where the defender never stood does no damage", delta =>
        {
            var attackerAt = BoardGeometry.Center(Square.Parse("e4"));
            var defenderAt = BoardGeometry.Center(Square.Parse("d5"));
            _a.SendPose(attackerAt);
            _b.SendPose(defenderAt);
            if (!_cheated)
            {
                _cheated = true;
                _a.Fire(EyeOf(attackerAt), [defenderAt + new Vector3(4f, 1f, 0f)], hitMask: 1);
            }
            if ((_waited += delta) < 0.8) return false;
            Check(_b.DamageTaken == 0, $"a fabricated hit did {_b.DamageTaken} damage");
            return true;
        });
        Then("a headshot deals one and a half times the damage", delta =>
        {
            var attackerAt = BoardGeometry.Center(Square.Parse("e4"));
            var defenderAt = BoardGeometry.Center(Square.Parse("d5"));
            _a.SendPose(attackerAt);
            _b.SendPose(defenderAt);
            if (!_headshotFired)
            {
                _headshotFired = true;
                var height = PieceModels.Height(PieceType.Pawn);
                _a.Fire(EyeOf(attackerAt), [defenderAt + Vector3.Up * height * 0.93f], hitMask: 1);
            }
            if (_b.DamageTaken == 0) return false;
            Check(_b.HeadshotTaken, "the server did not report a headshot");
            Check(Mathf.IsEqualApprox(_b.DamageTaken, 27f), $"a pawn headshot did {_b.DamageTaken} instead of 27");
            return true;
        });
        Then("hits on a moving target count where the shooter saw it", delta =>
        {
            var attackerAt = BoardGeometry.Center(Square.Parse("e4"));
            var defenderAt = BoardGeometry.Center(Square.Parse("d5"));
            _moveTime += delta;
            // B strafes sideways at up to about 6 m/s.
            var position = defenderAt + new Vector3(Mathf.Sin((float)_moveTime * 3f) * 2f, 0, 0);
            _bTrail.Add((_moveTime, position));
            _a.SendPose(attackerAt);
            _b.SendPose(position);

            _sinceShot += delta;
            if (_sinceShot > 0.3 && _moveTime > 0.5)
            {
                _sinceShot = 0;
                // Where A saw B: 0.15 s in the past, like a client behind the network and its smoothing.
                var seen = _bTrail.Last(p => p.Time <= _moveTime - 0.15).Position;
                var height = PieceModels.Height(PieceType.Pawn);
                _a.Fire(EyeOf(attackerAt), [seen + Vector3.Up * Ballistics.ChestHeight(height)], hitMask: 1);
            }
            return _a.DuelEnded is not null && _b.DuelEnded is not null && _a.Moves.Count == 3 && _b.Moves.Count == 3;
        });
        Then("the duel result is shared and applied", _ =>
        {
            Check(_a.DuelEnded == (int)DuelOutcome.AttackerWon, $"duel outcome was {_a.DuelEnded}");
            Check(_b.Moves[2] == ("e4d5", (int)DuelOutcome.AttackerWon), $"B saw {_b.Moves[2]}");
            Check(_b.DamageTaken > 27, "B never saw the body hits after the headshot");
            return true;
        });
        Then("B loses the connection and A is told", _ =>
        {
            if (!_b.Dropped)
            {
                _b.Dropped = true;
                _a.Notices.Clear();
                NetHost.Close(_b.Client);
            }
            return _a.Notices.Any(n => n.Contains("Verbindung verloren"));
        });
        Then("B comes back with its token and gets the game state", _ =>
        {
            _bBack ??= TestPlayer.Connect(this, _url, "Bob");
            if (!_bBack.Welcomed) return false;
            if (!_bBack.JoinRequested)
            {
                _bBack.JoinRequested = true;
                _bBack.Client.Rejoin(_roomCode, _b.Client.SeatToken!);
            }
            return _bBack.Color == PieceColor.Black && _bBack.Game is { Moves.Count: 3 } && _a.Notices.Any(n => n.Contains("wieder verbunden"));
        });
        Then("a wrong token cannot take a seat", _ =>
        {
            var intruder = TestPlayer.Connect(this, _url, "Mallory");
            _intruder = intruder;
            return true;
        });
        Then("the intruder is refused", _ =>
        {
            if (!_intruder!.Welcomed) return false;
            if (!_intruder.JoinRequested)
            {
                _intruder.JoinRequested = true;
                _intruder.Client.Rejoin(_roomCode, "not-a-token");
            }
            return _intruder.Errors.Count > 0 && _intruder.Color is null;
        });
        Then("resigning after the reconnect ends the game for both", _ =>
        {
            if (!_bBack!.Resigned)
            {
                _bBack.Resigned = true;
                _bBack.Channel.RpcId(1, MatchChannel.MethodName.Resign);
            }
            return _a.LastState?.ForfeitStatus == "Resignation" && _bBack.LastState?.ForfeitStatus == "Resignation";
        });

        Then("a rematch request is shown to the opponent", _ =>
        {
            if (!_rematchAsked)
            {
                _rematchAsked = true;
                _bBack!.Notices.Clear();
                _a.Channel.RpcId(1, MatchChannel.MethodName.RequestRematch);
            }
            return _bBack!.Notices.Any(n => n.Contains("Revanche"));
        });
        Then("when both agree a fresh game starts with colours swapped", _ =>
        {
            if (!_rematchAccepted)
            {
                _rematchAccepted = true;
                _bBack!.Channel.RpcId(1, MatchChannel.MethodName.RequestRematch);
            }
            return _a.Color == PieceColor.Black && _bBack!.Color == PieceColor.White &&
                   _a.LastState is { Moves.Count: 0, ForfeitStatus: null } && _bBack.LastState is { Moves.Count: 0, ForfeitStatus: null };
        });
        Then("the rematch is playable", _ =>
        {
            if (!_bBack!.Sent.Contains("d2d4")) _bBack.Submit("d2d4");
            return _a.Moves.Count > 3 && _a.Moves[^1].Uci == "d2d4";
        });

        // ---- scenario 2: against the computer, which must fight on its own
        Then("C starts a computer game from a save", _ =>
        {
            _c ??= TestPlayer.Connect(this, _url, "Carol");
            if (!_c.Welcomed) return false;
            if (!_c.CreatedRequest)
            {
                _c.CreatedRequest = true;
                var setup = new GameSetup(PlayerKind.Computer, PlayerKind.Human, 4, null, GameVariant.Duel);
                var save = GameSerializer.Serialize(new SavedGame(setup, new Game("4k3/8/8/3q4/8/8/3R4/4K3 w - - 0 1", GameVariant.Duel), null));
                _c.Client.CreateRoom(new RoomSettings { PlayerName = "Carol", VsComputer = true, SaveJson = save, Public = false });
            }
            return _c.Game is not null;
        });
        Then("the computer captures and fights the duel to the end", _ =>
        {
            if (_c.Duel is not null) _c.SendPose(BoardGeometry.Center(Square.Parse("d5")));
            return _c.DuelEnded is not null && _c.Moves.Count == 1;
        });
        Then("the computer's bot actually shot", _ =>
        {
            Check(_c.DamageTaken > 0 || _c.DuelEnded == (int)DuelOutcome.DefenderWon, "the bot never hit an opponent standing still");
            return true;
        });

        NextStep();
    }

    private void Then(string name, Func<double, bool> step) => _steps.Enqueue((name, step));

    private void NextStep()
    {
        if (_steps.Count == 0)
        {
            Finish();
            return;
        }
        (_currentStep, _step) = _steps.Dequeue();
        GD.Print($"[test] {_currentStep}");
    }

    public override void _Process(double delta)
    {
        if (_step is null) return;
        _elapsed += delta;
        if (_elapsed > _timeoutSeconds)
        {
            Fail($"timed out in step '{_currentStep}'");
            Finish();
            return;
        }
        if (_step(delta)) NextStep();
    }

    private void Check(bool condition, string message)
    {
        if (!condition) Fail($"{_currentStep}: {message}");
    }

    private void Fail(string message)
    {
        _failures.Add(message);
        GD.PrintErr($"[test] FAIL {message}");
    }

    private void Finish()
    {
        _step = null;
        GD.Print(_failures.Count == 0 ? "[test] ALL PASSED" : $"[test] {_failures.Count} FAILED");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }

    /// <summary>A scripted player: a real <see cref="NetClient"/> plus a recorder for everything the server sends.</summary>
    private sealed class TestPlayer : IMatchClient
    {
        public NetClient Client { get; private init; } = null!;
        public MatchChannel Channel => Client.Channel!;
        public bool Welcomed;
        public bool CreatedRequest;
        public bool ListRequested;
        public bool JoinRequested;
        public bool Resigned;
        public bool Dropped;
        public bool Disconnected;
        public readonly List<string> Errors = [];
        public PieceColor? Color;
        public List<RoomListEntry>? Rooms;
        public int States;
        public MatchStateDto? LastState;
        public Game? Game;
        public readonly List<(string Uci, int Outcome)> Moves = [];
        public readonly List<string> Sent = [];
        public readonly List<string> Notices = [];
        public DuelStartDto? Duel;
        public int? DuelEnded;
        public float DamageTaken;
        private DuelRole _role;
        private double _sincePose;

        public static TestPlayer Connect(Node parent, string url, string name)
        {
            var client = NetClient.Connect(parent, url, name);
            var player = new TestPlayer { Client = client };
            client.Welcomed += () => player.Welcomed = true;
            client.Failed += message => player.Errors.Add(message);
            client.RoomJoined += (_, color) =>
            {
                player.Color = color;
                client.Channel!.Client = player;
            };
            client.RoomListReceived += rooms => player.Rooms = rooms;
            client.Disconnected += () => player.Disconnected = true;
            return player;
        }

        public void Submit(string uci)
        {
            Sent.Add(uci);
            Channel.RpcId(1, MatchChannel.MethodName.SubmitMove, uci);
        }

        public void SendPose(Vector3 position)
        {
            if (Duel is null || DuelEnded is not null) return;
            if ((_sincePose += 1.0 / 60.0) < NetConfig.SyncInterval) return;
            _sincePose = 0;
            Channel.RpcId(1, MatchChannel.MethodName.DuelInput, position, 0f, 0f);
        }

        public void Fire(Vector3 origin, Vector3[] ends, int hitMask) => Channel.RpcId(1, MatchChannel.MethodName.Fire, origin, ends, hitMask);

        public void OnState(string json)
        {
            States++;
            LastState = NetJson.Read<MatchStateDto>(json);
            Game = GameSerializer.Replay(LastState.StartFen, GameVariant.Duel, LastState.Moves);
        }

        public void OnMovePlayed(string uci, int outcome) => Moves.Add((uci, outcome));

        public void OnDuelStarted(string json)
        {
            Duel = NetJson.Read<DuelStartDto>(json);
            _role = Enum.Parse<PieceColor>(Duel.AttackerColor) == Color ? DuelRole.Attacker : DuelRole.Defender;
        }

        public bool Fighting;

        public void OnDuelSnapshot(Vector3 attacker, float attackerYaw, float attackerPitch, Vector3 defender, float defenderYaw, float defenderPitch, float[] stats) =>
            Fighting = stats[SnapshotField.IntroLeft] <= 0f && (int)stats[SnapshotField.Phase] == (int)DuelPhase.Fighting;
        public void OnShotFired(int role, Vector3 origin, Vector3[] ends) { }

        public bool HeadshotTaken;

        public void OnDamaged(int role, float amount, bool headshot)
        {
            if ((DuelRole)role != _role) return;
            DamageTaken += amount;
            HeadshotTaken |= headshot;
        }

        public void OnGrenadeThrown(int role, Vector3 origin, Vector3 velocity) { }
        public void OnGrenadeExploded(Vector3 position) { }
        public void OnDuelEnded(int outcome) => DuelEnded = outcome;
        public void OnSaveData(string json) { }
        public void OnNotice(string text) => Notices.Add(text);
        public void OnClockSync(long whiteMs, long blackMs) { }

        public readonly List<(string From, int Message)> Chats = [];

        public void OnChatReceived(string from, int message) => Chats.Add((from, message));
    }
}
