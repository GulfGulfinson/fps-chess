using Chess.Core;
using Chess.Core.Persistence;
using FpsChess.Net;
using FpsChess.Ui;
using Godot;

namespace FpsChess.App;

public partial class MainMenu : Control
{
    private static readonly (string Label, TimeControl Control)[] TimePresets =
    [
        ("Bullet 1 + 0", new TimeControl(TimeSpan.FromMinutes(1), TimeSpan.Zero)),
        ("Blitz 3 + 2", new TimeControl(TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(2))),
        ("Blitz 5 + 0", new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.Zero)),
        ("Schnellschach 10 + 5", new TimeControl(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5))),
        ("Klassisch 30 + 0", new TimeControl(TimeSpan.FromMinutes(30), TimeSpan.Zero)),
    ];

    private Main _main = null!;
    private string? _initialMessage;

    private OptionButton _view = null!;
    private OptionButton _opponent = null!;
    private OptionButton _aiLevel = null!;
    private CheckButton _timed = null!;
    private OptionButton _timePreset = null!;
    private Label _message = null!;
    private Control _setupPanel = null!;
    private Control _loadPanel = null!;
    private Control _onlinePanel = null!;
    private LineEdit _playerName = null!;
    private LineEdit _server = null!;
    private ItemList _saves = null!;
    private IReadOnlyList<SaveStore.Entry> _entries = [];

    public void Init(Main main, string? message)
    {
        _main = main;
        _initialMessage = message;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        center.AddChild(column);

        var title = UiTheme.Label("FPS CHESS", 64, UiTheme.Accent, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        var subtitle = UiTheme.Label("Schach, klassisch oder aus der Ego-Perspektive", 20, UiTheme.Muted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(subtitle);

        _setupPanel = BuildSetupPanel();
        column.AddChild(_setupPanel);
        _loadPanel = BuildLoadPanel();
        _loadPanel.Visible = false;
        column.AddChild(_loadPanel);
        _onlinePanel = BuildOnlinePanel();
        _onlinePanel.Visible = false;
        column.AddChild(_onlinePanel);

        _message = UiTheme.Label(_initialMessage ?? "", 16, UiTheme.Accent);
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(560, 0);
        column.AddChild(_message);
    }

    public override void _Draw()
    {
        // Faint oversized chessboard behind the menu.
        DrawRect(new Rect2(Vector2.Zero, Size), UiTheme.Background);
        const float cell = 96f;
        for (var y = 0; y * cell < Size.Y; y++)
        for (var x = 0; x * cell < Size.X; x++)
        {
            if ((x + y) % 2 == 0) DrawRect(new Rect2(x * cell, y * cell, cell, cell), new Color(1, 1, 1, 0.025f));
        }
    }

    private Control BuildSetupPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 14);

        _view = Options("2D-Brett (klassisch)", "FPS Chess (Duelle)");
        _opponent = Options("Mensch gegen Mensch", "Gegen Computer, ich spiele Weiß", "Gegen Computer, ich spiele Schwarz");
        _aiLevel = Options(Enumerable.Range(GameSetup.MinAiLevel, GameSetup.MaxAiLevel).Select(Texts.AiLevel).ToArray());
        _aiLevel.Selected = 2;
        _timed = new CheckButton { Text = "Schachuhr (Timed Mode)" };
        _timePreset = Options(TimePresets.Select(p => p.Label).ToArray());
        _timePreset.Selected = 2;

        _opponent.ItemSelected += _ => UpdateEnabled();
        _view.ItemSelected += _ => UpdateEnabled();
        _timed.Toggled += _ => UpdateEnabled();

        AddRow(grid, "Ansicht", _view);
        AddRow(grid, "Spieler", _opponent);
        AddRow(grid, "Computer-Stärke", _aiLevel);
        AddRow(grid, "Zeitmodus", _timed);
        AddRow(grid, "Bedenkzeit", _timePreset);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Neues Spiel", StartNewGame, 160));
        buttons.AddChild(UiTheme.Button("Spiel laden", ShowLoadPanel, 160));
        buttons.AddChild(UiTheme.Button("Online spielen", ShowOnlinePanel, 170));
        buttons.AddChild(UiTheme.Button("Beenden", () => GetTree().Quit(), 120));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 22);
        column.AddChild(grid);
        column.AddChild(buttons);
        panel.AddChild(column);

        UpdateEnabled();
        return panel;
    }

    private Control BuildLoadPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);

        column.AddChild(UiTheme.Label("Gespeicherte Spiele", 22, UiTheme.Accent, bold: true));
        column.AddChild(UiTheme.Label("FPS-Chess-Partien werden mit Duellen fortgesetzt, klassische auf dem 2D-Brett.", 15, UiTheme.Muted));

        _saves = new ItemList { CustomMinimumSize = new Vector2(0, 300) };
        _saves.ItemActivated += _ => LoadSelected();
        column.AddChild(_saves);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Laden", LoadSelected, 140));
        buttons.AddChild(UiTheme.Button("Löschen", DeleteSelected, 140));
        buttons.AddChild(UiTheme.Button("Zurück", ShowSetupPanel, 140));
        column.AddChild(buttons);

        panel.AddChild(column);
        return panel;
    }

    private Control BuildOnlinePanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(640, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        column.AddChild(UiTheme.Label("FPS Chess online", 22, UiTheme.Accent, bold: true));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 14);
        _playerName = new LineEdit { Text = UserPrefs.PlayerName, MaxLength = 24, CustomMinimumSize = new Vector2(400, 44) };
        _server = new LineEdit { Text = UserPrefs.ServerUrl, CustomMinimumSize = new Vector2(400, 44) };
        AddRow(grid, "Dein Name", _playerName);
        AddRow(grid, "Server", _server);
        column.AddChild(grid);

        var hint = UiTheme.Label(
            $"Öffentlicher Server: {NetConfig.DefaultServerUrl}. Für ein Spiel im LAN hostet einer, die anderen tragen dessen Adresse ein (ws://IP:{NetConfig.DefaultPort}).",
            15, UiTheme.Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(600, 0);
        column.AddChild(hint);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.AddChild(UiTheme.Button("Verbinden", ConnectOnline, 150));
        buttons.AddChild(UiTheme.Button("Im LAN hosten", HostLan, 170));
        buttons.AddChild(UiTheme.Button("Zurück", ShowSetupPanel, 120));
        column.AddChild(buttons);
        panel.AddChild(column);
        return panel;
    }

    private void ShowOnlinePanel()
    {
        _setupPanel.Visible = false;
        _loadPanel.Visible = false;
        _onlinePanel.Visible = true;
        _message.Text = "";
    }

    private string RememberName()
    {
        var name = string.IsNullOrWhiteSpace(_playerName.Text) ? "Spieler" : _playerName.Text.Trim();
        UserPrefs.PlayerName = name;
        return name;
    }

    private void ConnectOnline()
    {
        var url = _server.Text.Trim();
        if (!url.StartsWith("ws://") && !url.StartsWith("wss://"))
        {
            _message.Text = "Die Serveradresse muss mit ws:// oder wss:// beginnen.";
            return;
        }
        UserPrefs.ServerUrl = url;
        _main.OpenOnlineLobby(url, RememberName());
    }

    private void HostLan() => _main.HostLan(RememberName());

    private static OptionButton Options(params string[] items)
    {
        var options = new OptionButton { CustomMinimumSize = new Vector2(320, 44) };
        foreach (var item in items) options.AddItem(item);
        return options;
    }

    private static void AddRow(GridContainer grid, string label, Control control)
    {
        grid.AddChild(UiTheme.Label(label, 18, UiTheme.Muted));
        grid.AddChild(control);
    }

    private void UpdateEnabled()
    {
        // FPS Chess between two people needs two devices (online play), so locally it is against the computer.
        var fps = SelectedView == ViewMode.Fps;
        _opponent.SetItemDisabled(0, fps);
        if (fps && _opponent.Selected == 0) _opponent.Selected = 1;
        _aiLevel.Disabled = _opponent.Selected == 0;
        _timePreset.Disabled = !_timed.ButtonPressed;
    }

    private void StartNewGame()
    {
        var (white, black) = _opponent.Selected switch
        {
            1 => (PlayerKind.Human, PlayerKind.Computer),
            2 => (PlayerKind.Computer, PlayerKind.Human),
            _ => (PlayerKind.Human, PlayerKind.Human),
        };
        var timeControl = _timed.ButtonPressed ? TimePresets[_timePreset.Selected].Control : null;
        var aiLevel = _aiLevel.Selected + GameSetup.MinAiLevel;

        if (SelectedView == ViewMode.Fps)
        {
            _main.StartComputerMatch(new RoomSettings
            {
                PlayerName = "Du",
                AiLevel = aiLevel,
                HumanColor = white == PlayerKind.Human ? "White" : "Black",
                InitialMs = (long?)timeControl?.Initial.TotalMilliseconds,
                IncrementMs = (long?)timeControl?.Increment.TotalMilliseconds,
            });
            return;
        }
        _main.StartGame(new GameSetup(white, black, aiLevel, timeControl));
    }

    private ViewMode SelectedView => _view.Selected == 0 ? ViewMode.Board2D : ViewMode.Fps;

    private void ShowLoadPanel()
    {
        RefreshSaves();
        _setupPanel.Visible = false;
        _loadPanel.Visible = true;
        _message.Text = _entries.Count == 0 ? $"Noch keine Spielstände in {SaveStore.DirectoryPath}." : "";
    }

    private void ShowSetupPanel()
    {
        _loadPanel.Visible = false;
        _onlinePanel.Visible = false;
        _setupPanel.Visible = true;
        _message.Text = "";
    }

    private void RefreshSaves()
    {
        _entries = SaveStore.List();
        _saves.Clear();
        foreach (var entry in _entries) _saves.AddItem(SaveStore.Describe(entry));
        if (_entries.Count > 0) _saves.Select(0);
    }

    private SaveStore.Entry? SelectedEntry
    {
        get
        {
            var selected = _saves.GetSelectedItems();
            return selected.Length == 0 ? null : _entries[selected[0]];
        }
    }

    private void LoadSelected()
    {
        if (SelectedEntry is not { } entry)
        {
            _message.Text = "Bitte zuerst einen Spielstand auswählen.";
            return;
        }
        try
        {
            var save = SaveStore.Load(entry);
            if (save.Setup.Variant == GameVariant.Duel)
                _main.StartComputerMatch(new RoomSettings { PlayerName = "Du", SaveJson = SaveStore.ReadJson(entry) });
            else
                _main.LoadGame(save);
        }
        catch (Exception e) when (e is InvalidSaveFileException or IOException)
        {
            _message.Text = $"Laden fehlgeschlagen: {e.Message}";
        }
    }

    private void DeleteSelected()
    {
        if (SelectedEntry is not { } entry) return;
        SaveStore.Delete(entry);
        RefreshSaves();
        _message.Text = $"\"{entry.Name}\" gelöscht.";
    }
}
