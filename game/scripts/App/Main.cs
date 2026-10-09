using Chess.Core;
using Chess.Core.Persistence;
using Chess.Core.Session;
using FpsChess.Board2D;
using FpsChess.Fps;
using FpsChess.Ui;
using Godot;

namespace FpsChess.App;

/// <summary>Root node: owns the running <see cref="GameSession"/> and swaps between menu, 2D board and FPS world.</summary>
public partial class Main : Node
{
    private Node? _screen;
    private GameSession? _session;
    private ViewMode _view;

    /// <summary>The running game, if any. Exposed for the screenshot tool.</summary>
    public GameSession? Session => _session;

    public override void _Ready()
    {
        InputSetup.Register();
        ShowMenu();
    }

    public override void _ExitTree() => _session?.Dispose();

    public void ShowMenu(string? message = null)
    {
        EndSession();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var menu = new MainMenu();
        menu.Init(this, message);
        SetScreen(menu);
    }

    public void StartGame(ViewMode view, GameSetup setup) => Begin(view, new GameSession(setup));

    public void LoadGame(ViewMode view, SavedGame save) => Begin(view, new GameSession(save.Setup, save.Game, save.Clock));

    /// <summary>New game with the same view and players as the current one.</summary>
    public void Rematch()
    {
        if (_session is null) return;
        StartGame(_view, _session.Setup);
    }

    /// <returns>A message for the user, either the save name or the error.</returns>
    public string SaveCurrentGame()
    {
        if (_session is null) return "Kein Spiel aktiv.";
        try
        {
            return $"Gespeichert als \"{SaveStore.Save(_session.ToSave())}\".";
        }
        catch (IOException e)
        {
            GD.PushError($"Saving failed: {e}");
            return $"Speichern fehlgeschlagen: {e.Message}";
        }
    }

    private void Begin(ViewMode view, GameSession session)
    {
        EndSession();
        _session = session;
        _view = view;
        if (view == ViewMode.Board2D)
        {
            var screen = new Game2DScreen();
            screen.Init(this, session);
            SetScreen(screen);
        }
        else
        {
            var screen = new FpsScreen();
            screen.Init(this, session);
            SetScreen(screen);
        }
    }

    private void EndSession()
    {
        _session?.Dispose();
        _session = null;
    }

    private void SetScreen(Node screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }
}
