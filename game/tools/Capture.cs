using Chess.Core;
using FpsChess.App;
using Godot;

namespace FpsChess.Tools;

/// <summary>
/// Development tool, not part of the game: starts a screen, optionally plays moves and saves a screenshot.
/// godot --path game res://tools/Capture.tscn -- --mode 2d|fps|menu --moves e2e4,e7e5 --out /tmp/shot.png
/// </summary>
public partial class Capture : Node
{
    private Main _main = null!;
    private string _out = "capture.png";
    private int _frames = 90;

    public override void _Ready()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
        _out = args.GetValueOrDefault("out", _out);
        if (args.TryGetValue("frames", out var frames)) _frames = int.Parse(frames);

        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<Main>();
        AddChild(_main);

        var mode = args.GetValueOrDefault("mode", "menu");
        if (mode != "menu")
        {
            CallDeferred(MethodName.StartMode, (int)(mode == "fps" ? ViewMode.Fps : ViewMode.Board2D),
                args.GetValueOrDefault("moves", ""), args.GetValueOrDefault("opponent", "") == "computer", args.ContainsKey("timed"));
        }
    }

    private void StartMode(int view, string moves, bool computer, bool timed)
    {
        var setup = new GameSetup(PlayerKind.Human, computer ? PlayerKind.Computer : PlayerKind.Human, 2,
            timed ? new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3)) : null);
        _main.StartGame((ViewMode)view, setup);
        foreach (var uci in moves.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_main.Session!.TryPlayHumanMove(Move.ParseUci(uci))) GD.PushError($"Capture: move {uci} rejected");
        }
    }

    public override void _Process(double delta)
    {
        if (--_frames > 0) return;
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(_out);
        GD.Print($"Capture saved to {_out}");
        GetTree().Quit();
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
