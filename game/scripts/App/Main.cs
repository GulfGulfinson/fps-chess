using Chess.Core;
using Chess.Core.Persistence;
using Chess.Core.Session;
using FpsChess.Board2D;
using FpsChess.Match;
using FpsChess.Net;
using FpsChess.Net.Client;
using FpsChess.Net.Server;
using FpsChess.Ui;
using Godot;

namespace FpsChess.App;

/// <summary>
/// Root node: swaps between menu, 2D board and FPS Chess. The 2D mode runs a local <see cref="GameSession"/>;
/// FPS Chess always talks to a match server, embedded in this process for games against the computer.
/// </summary>
public partial class Main : Node
{
    private Node? _screen;
    private GameSession? _session;
    private LobbyServer? _localServer;
    private NetClient? _client;
    private RoomSettings? _lastMatch;

    /// <summary>The running 2D game, if any. Exposed for the screenshot tool.</summary>
    public GameSession? Session => _session;

    /// <summary>The running FPS Chess screen, if any. Exposed for the screenshot tool.</summary>
    public MatchScreen? Match => _screen as MatchScreen;

    public override void _Ready()
    {
        InputSetup.Register();
        ShowMenu();
    }

    public override void _ExitTree() => EndAll();

    public void ShowMenu(string? message = null)
    {
        EndAll();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        var menu = new MainMenu();
        menu.Init(this, message);
        SetScreen(menu);
    }

    // ---------------------------------------------------------------- 2D mode

    public void StartGame(GameSetup setup) => Begin2D(new GameSession(setup));

    public void LoadGame(SavedGame save) => Begin2D(new GameSession(save.Setup, save.Game, save.Clock));

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

    private void Begin2D(GameSession session)
    {
        EndAll();
        _session = session;
        var screen = new Game2DScreen();
        screen.Init(this, session);
        SetScreen(screen);
    }

    // ---------------------------------------------------------------- FPS Chess

    /// <summary>Starts a game against the computer on a match server inside this process.</summary>
    public void StartComputerMatch(RoomSettings settings)
    {
        EndAll();
        _lastMatch = settings;
        settings.VsComputer = true;
        settings.Public = false;

        var port = 0;
        for (var attempt = 0; attempt < 8 && _localServer is null; attempt++)
        {
            port = Random.Shared.Next(30000, 60000);
            var server = LobbyServer.Start(this, port, "127.0.0.1", out var error);
            if (error == Error.Ok) _localServer = server;
            else server.QueueFree();
        }
        if (_localServer is null)
        {
            ShowMenu("Der lokale Spielserver konnte nicht gestartet werden.");
            return;
        }

        ShowConnecting("Starte Partie ...");
        Connect($"ws://127.0.0.1:{port}", settings.PlayerName, client => client.CreateRoom(settings));
    }

    /// <summary>New game with the same settings as the one that just ended.</summary>
    public void Rematch()
    {
        if (_session is { } session) StartGame(session.Setup);
        else if (_lastMatch is { } match) StartComputerMatch(new RoomSettings
        {
            PlayerName = match.PlayerName,
            AiLevel = match.AiLevel,
            HumanColor = match.HumanColor,
            InitialMs = match.InitialMs,
            IncrementMs = match.IncrementMs,
        });
    }

    private void Connect(string url, string playerName, Action<NetClient> onWelcome)
    {
        var client = NetClient.Connect(this, url, playerName);
        _client = client;
        client.Welcomed += () => onWelcome(client);
        client.Failed += message => ShowMenu(message);
        client.Disconnected += () => ShowMenu("Die Verbindung zum Spielserver ist abgebrochen.");
        client.RoomJoined += (_, color) =>
        {
            var screen = new MatchScreen();
            screen.Init(this, client, color);
            SetScreen(screen);
            client.Channel!.Client = screen;
        };
    }

    private void ShowConnecting(string text)
    {
        var screen = new Control();
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var background = new ColorRect { Color = UiTheme.Background };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(background);
        var label = UiTheme.Label(text, 28, UiTheme.Accent, bold: true);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        screen.AddChild(label);
        SetScreen(screen);
    }

    // ---------------------------------------------------------------- lifecycle

    private void EndAll()
    {
        _session?.Dispose();
        _session = null;

        if (_client is not null)
        {
            // Close now: the node itself is freed at the end of the frame.
            NetHost.Close(_client);
            _client.QueueFree();
            _client = null;
        }
        if (_localServer is not null)
        {
            NetHost.Close(_localServer);
            _localServer.QueueFree();
            _localServer = null;
        }
    }

    private void SetScreen(Node screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }
}
