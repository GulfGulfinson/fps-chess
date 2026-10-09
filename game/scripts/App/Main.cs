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

    private OnlineContext? _online;

    /// <summary>Where an online game runs, so a dropped connection can be resumed.</summary>
    private sealed record OnlineContext(string Url, string Name, string? LanHint);

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--server"))
        {
            RunDedicatedServer(args);
            return;
        }
        InputSetup.Register();
        ShowMenu();
    }

    /// <summary>
    /// Headless server mode for gulfinson: godot --headless --path game -- --server [--port 11800] [--bind 127.0.0.1]
    /// </summary>
    private void RunDedicatedServer(string[] args)
    {
        string Arg(string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        var port = int.TryParse(Arg("--port", ""), out var parsed) ? parsed : NetConfig.DefaultPort;
        var bind = Arg("--bind", "127.0.0.1");
        _localServer = LobbyServer.Start(this, port, bind, out var error);
        if (error != Error.Ok)
        {
            GD.PushError($"[server] could not listen on {bind}:{port}: {error}");
            GetTree().Quit(1);
        }
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

    private NetClient Connect(string url, string playerName, Action<NetClient> onWelcome)
    {
        var client = NetClient.Connect(this, url, playerName);
        _client = client;
        client.Welcomed += () => onWelcome(client);
        client.Failed += message =>
        {
            // In the lobby the screen shows the error itself.
            if (_screen is not LobbyScreen) ShowMenu(message);
        };
        client.Disconnected += () => OnDisconnected(client);
        client.RoomJoined += (_, color) =>
        {
            var screen = new MatchScreen();
            screen.Init(this, client, color, _online?.LanHint);
            SetScreen(screen);
            client.Channel!.Client = screen;
        };
        return client;
    }

    // ---------------------------------------------------------------- online

    public void OpenOnlineLobby(string url, string playerName) => OpenLobby(url, playerName, url, null);

    /// <summary>Hosts a server reachable from the local network and opens its lobby.</summary>
    public void HostLan(string playerName)
    {
        EndAll();
        _localServer = LobbyServer.Start(this, NetConfig.DefaultPort, "0.0.0.0", out var error);
        if (error != Error.Ok)
        {
            ShowMenu($"Port {NetConfig.DefaultPort} ist belegt oder gesperrt ({error}).");
            return;
        }
        var addresses = IP.GetLocalAddresses()
            .Where(a => a.Contains('.') && !a.StartsWith("127.") && !a.StartsWith("169.254."))
            .Select(a => $"ws://{a}:{NetConfig.DefaultPort}");
        var hint = $"Du hostest im LAN. Mitspieler tragen als Server ein: {string.Join(" oder ", addresses)}";
        OpenLobby($"ws://127.0.0.1:{NetConfig.DefaultPort}", playerName, "deinem LAN-Server", hint, keepServer: true);
    }

    private void OpenLobby(string url, string playerName, string label, string? hint, bool keepServer = false)
    {
        if (!keepServer) EndAll();
        else EndClient();
        _online = new OnlineContext(url, playerName, hint);
        var client = Connect(url, playerName, _ => { });
        var lobby = new LobbyScreen();
        lobby.Init(this, client, label, hint);
        SetScreen(lobby);
    }

    private void OnDisconnected(NetClient client)
    {
        if (client != _client) return;
        // Online games keep the seat for a while: try to get back in.
        if (_online is { } online && client.RoomCode is { } code && client.SeatToken is { } token && _screen is MatchScreen)
        {
            Reconnect(online, code, token, Time.GetTicksMsec());
            return;
        }
        ShowMenu("Die Verbindung zum Spielserver ist abgebrochen.");
    }

    private void Reconnect(OnlineContext online, string code, string token, ulong startedMs)
    {
        EndClient();
        var elapsed = (Time.GetTicksMsec() - startedMs) / 1000.0;
        if (elapsed > NetConfig.ReconnectSeconds)
        {
            ShowMenu("Die Verbindung zum Spielserver ist abgebrochen.");
            return;
        }

        ShowConnecting($"Verbindung verloren. Verbinde neu ... ({NetConfig.ReconnectSeconds - elapsed:0} s)");
        var client = NetClient.Connect(this, online.Url, online.Name);
        _client = client;
        client.Welcomed += () => client.Rejoin(code, token);
        client.RoomJoined += (_, color) =>
        {
            var screen = new MatchScreen();
            screen.Init(this, client, color, online.LanHint);
            SetScreen(screen);
            client.Channel!.Client = screen;
        };
        client.Disconnected += () => OnDisconnected(client);
        // Server unreachable or the seat is gone: try again shortly, until the seat would be lost anyway.
        client.Failed += message =>
        {
            if (message.Contains("gibt es nicht mehr"))
            {
                ShowMenu(message);
                return;
            }
            var retry = CreateTween();
            retry.TweenInterval(3);
            retry.TweenCallback(Callable.From(() => Reconnect(online, code, token, startedMs)));
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
        _online = null;

        EndClient();
        if (_localServer is not null)
        {
            NetHost.Close(_localServer);
            _localServer.QueueFree();
            _localServer = null;
        }
    }

    private void EndClient()
    {
        if (_client is null) return;
        // Close now: the node itself is freed at the end of the frame.
        var client = _client;
        _client = null;
        NetHost.Close(client);
        client.QueueFree();
    }

    private void SetScreen(Node screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }
}
