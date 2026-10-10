using Chess.Core;
using Chess.Core.Persistence;
using FpsChess.App;
using FpsChess.Net;
using FpsChess.Net.Client;
using Godot;

namespace FpsChess.Tools;

/// <summary>
/// Development tool, not part of the game: starts a screen, optionally plays moves and saves screenshots.
///   godot --path game res://tools/Capture.tscn -- --mode menu|2d|fps --out /tmp/shot.png [--shots 60,300]
///   2d:  --moves e2e4,e7e5 [--timed]
///   fps: [--fen FEN] [--human white|black] [--submit e4d5] [--level 2] [--timed] [--fire FRAME]
/// With --shots, one screenshot per listed frame is written as out_FRAME.png and the tool quits after the last.
/// </summary>
public partial class Capture : Node
{
    private Main _main = null!;
    private string _out = "capture.png";
    private readonly SortedSet<int> _shots = [];
    private string? _submit;
    private double _fireAt = -1;
    private int _fireState;
    private int _frame;
    private ulong _benchStart;
    private readonly SortedSet<double> _times = [];
    private double _elapsed;
    private bool _submitted;
    private string? _open;

    public override void _Ready()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
        _out = args.GetValueOrDefault("out", _out);
        if (args.TryGetValue("times", out var times))
        {
            foreach (var time in times.Split(',')) _times.Add(double.Parse(time, System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            foreach (var shot in args.GetValueOrDefault("shots", "90").Split(',')) _shots.Add(int.Parse(shot));
        }
        _submit = args.GetValueOrDefault("submit");
        _open = args.GetValueOrDefault("open");
        if (args.TryGetValue("graphics", out var graphics)) UserPrefs.Graphics = Enum.Parse<GraphicsLevel>(graphics, ignoreCase: true);
        if (args.TryGetValue("fire", out var fire)) _fireAt = double.Parse(fire, System.Globalization.CultureInfo.InvariantCulture);

        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
        AddChild(_main);

        switch (args.GetValueOrDefault("mode", "menu"))
        {
            case "2d":
                CallDeferred(MethodName.Start2D, args.GetValueOrDefault("moves", ""), args.ContainsKey("timed"));
                break;
            case "lan":
                CallDeferred(MethodName.StartLan);
                break;
            case "fps":
                CallDeferred(MethodName.StartFps, args.GetValueOrDefault("fen", Position.StartFen),
                    args.GetValueOrDefault("human", "white"), int.Parse(args.GetValueOrDefault("level", "2")), args.ContainsKey("timed"));
                break;
        }
    }

    private void Start2D(string moves, bool timed)
    {
        _main.StartGame(new GameSetup(PlayerKind.Human, PlayerKind.Human, 2,
            timed ? new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3)) : null));
        foreach (var uci in moves.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_main.Session!.TryPlayHumanMove(Move.ParseUci(uci))) GD.PushError($"Capture: move {uci} rejected");
        }
    }

    private void StartLan() => _main.HostLan("Tester");

    private void StartFps(string fen, string human, int level, bool timed)
    {
        var humanColor = human == "black" ? PieceColor.Black : PieceColor.White;
        var setup = humanColor == PieceColor.White
            ? new GameSetup(PlayerKind.Human, PlayerKind.Computer, level, null, GameVariant.Duel)
            : new GameSetup(PlayerKind.Computer, PlayerKind.Human, level, null, GameVariant.Duel);
        var save = GameSerializer.Serialize(new SavedGame(setup, new Game(fen, GameVariant.Duel), null));
        _main.StartComputerMatch(new RoomSettings
        {
            PlayerName = "Tester",
            AiLevel = level,
            SaveJson = save,
            InitialMs = timed ? 300_000 : null,
            IncrementMs = timed ? 3_000 : null,
        });
    }

    public override void _Process(double delta)
    {
        _frame++;
        // LAN mode: create a room once the lobby is connected.
        if (_frame == 120 && _main.FindChild("Client", true, false) is NetClient lanClient && _main.Match is null && _submit == "create")
            lanClient.CreateRoom(new RoomSettings { PlayerName = "Tester", Public = true });

        if (!_submitted && _elapsed >= 1.5 && _submit is not null && _submit != "create" && _main.FindChild("Client", true, false) is NetClient { Channel: { } channel })
        {
            _submitted = true;
            foreach (var uci in _submit.Split(',')) channel.RpcId(1, MatchChannel.MethodName.SubmitMove, uci);
        }

        // From --fire seconds on: look at the opponent and hold the fire button for two seconds.
        if (_fireState == 1 && _main.FindChild("Opponent", true, false) is Node3D enemy &&
            _main.FindChild("Fighter", true, false) is FpsChess.Duel.LocalFighter me)
        {
            me.Camera.LookAt(enemy.GlobalPosition + Vector3.Up * 1f);
        }
        if (_fireAt >= 0 && _fireState < 2 && _elapsed >= _fireAt + _fireState * 2.0)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = _fireState == 0 });
            _fireState++;
        }

        // Benchmark: average frame rate over frames 120 to 420, measured in real time.
        if (_frame == 120) _benchStart = Time.GetTicksUsec();
        if (_frame == 420 && _benchStart > 0)
        {
            var seconds = (Time.GetTicksUsec() - _benchStart) / 1e6;
            GD.Print($"[bench] {300 / seconds:0.0} fps average, {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draw calls, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:0}k primitives, window {DisplayServer.WindowGetSize()}");
        }

        if (_frame % 30 == 0 && OS.GetCmdlineUserArgs().Contains("--perf"))
        {
            GD.Print($"[perf] frame {_frame}: {Engine.GetFramesPerSecond()} fps, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draw calls, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objects, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:0}k primitives, " +
                     $"process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms, " +
                     $"physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} ms, " +
                     $"window {DisplayServer.WindowGetSize()}");
        }

        if (_frame == 20 && _open is not null && _main.FindChild(_open, true, false) is Control panel) panel.Visible = true;

        // Time-based screenshots (seconds since start) for sequences, independent of the frame rate.
        _elapsed += delta;
        if (_times.Count > 0)
        {
            if (_elapsed < _times.Min) return;
            var at = _times.Min;
            _times.Remove(at);
            var timed = _out.Replace(".png", $"_{at:0.0}s.png");
            GetViewport().GetTexture().GetImage().SavePng(timed);
            GD.Print($"Capture saved to {timed} at frame {_frame}");
            if (_times.Count == 0) GetTree().Quit();
            return;
        }

        if (!_shots.Contains(_frame)) return;
        var path = _shots.Count > 1 || _shots.Min != 90 ? _out.Replace(".png", $"_{_frame}.png") : _out;
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"Capture saved to {path}");
        if (_frame >= _shots.Max) GetTree().Quit();
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
            result[key] = value;
        }
        return result;
    }
}
