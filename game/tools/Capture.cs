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
    private int _fireFrame = -1;
    private int _frame;

    public override void _Ready()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
        _out = args.GetValueOrDefault("out", _out);
        foreach (var shot in args.GetValueOrDefault("shots", "90").Split(',')) _shots.Add(int.Parse(shot));
        _submit = args.GetValueOrDefault("submit");
        if (args.TryGetValue("fire", out var fire)) _fireFrame = int.Parse(fire);

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

        if (_frame == 45 && _submit is not null && _submit != "create" && _main.FindChild("Client", true, false) is NetClient { Channel: { } channel })
        {
            foreach (var uci in _submit.Split(',')) channel.RpcId(1, MatchChannel.MethodName.SubmitMove, uci);
        }

        // Hold the fire button for a second, as a player would.
        if (_frame == _fireFrame || _frame == _fireFrame + 60)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = _frame == _fireFrame });
        }

        if (_frame % 30 == 0)
        {
            GD.Print($"[perf] frame {_frame}: {Engine.GetFramesPerSecond()} fps, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draw calls, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} objects, " +
                     $"{Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:0}k primitives, " +
                     $"process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms, " +
                     $"physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} ms, " +
                     $"window {DisplayServer.WindowGetSize()}");
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
