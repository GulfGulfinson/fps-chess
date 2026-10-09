using FpsChess.Net;
using FpsChess.Net.Client;
using FpsChess.Ui;
using Godot;

namespace FpsChess.App;

/// <summary>Online lobby: create a game, browse open games or join one by its code.</summary>
public partial class LobbyScreen : Control
{
    private static readonly (string Label, long? InitialMs, long? IncrementMs)[] TimeControls =
    [
        ("Ohne Uhr", null, null),
        ("Blitz 5 + 0", 300_000, 0),
        ("Schnellschach 10 + 5", 600_000, 5_000),
        ("Klassisch 30 + 0", 1_800_000, 0),
    ];

    private Main _main = null!;
    private NetClient _client = null!;
    private string _serverLabel = "";
    private string? _hint;

    private Label _status = null!;
    private OptionButton _time = null!;
    private OptionButton _color = null!;
    private ItemList _rooms = null!;
    private LineEdit _code = null!;
    private List<RoomListEntry> _entries = [];
    private bool _welcomed;

    public void Init(Main main, NetClient client, string serverLabel, string? hint)
    {
        _main = main;
        _client = client;
        _serverLabel = serverLabel;
        _hint = hint;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();

        var background = new ColorRect { Color = UiTheme.Background };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        column.AddThemeConstantOverride("separation", 16);
        center.AddChild(column);

        column.AddChild(UiTheme.Label("Online spielen", 40, UiTheme.Accent, bold: true));
        _status = UiTheme.Label($"Verbinde mit {_serverLabel} ...", 17, UiTheme.Muted);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_status);
        if (_hint is not null)
        {
            var hint = UiTheme.Label(_hint, 16, UiTheme.Accent);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(hint);
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        column.AddChild(row);

        // Create a game.
        var create = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var createColumn = new VBoxContainer();
        createColumn.AddThemeConstantOverride("separation", 12);
        createColumn.AddChild(UiTheme.Label("Neue Partie", 22, UiTheme.Accent, bold: true));
        _time = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
        foreach (var (label, _, _) in TimeControls) _time.AddItem(label);
        _color = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
        foreach (var label in new[] { "Ich spiele Weiß", "Ich spiele Schwarz", "Zufällige Farbe" }) _color.AddItem(label);
        createColumn.AddChild(UiTheme.Label("Bedenkzeit", 16, UiTheme.Muted));
        createColumn.AddChild(_time);
        createColumn.AddChild(UiTheme.Label("Farbe", 16, UiTheme.Muted));
        createColumn.AddChild(_color);
        createColumn.AddChild(UiTheme.Button("Partie erstellen", CreateRoom));
        create.AddChild(createColumn);
        row.AddChild(create);

        // Join a game.
        var join = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var joinColumn = new VBoxContainer();
        joinColumn.AddThemeConstantOverride("separation", 12);
        joinColumn.AddChild(UiTheme.Label("Offene Partien", 22, UiTheme.Accent, bold: true));
        _rooms = new ItemList { CustomMinimumSize = new Vector2(0, 200) };
        _rooms.ItemActivated += _ => JoinSelected();
        joinColumn.AddChild(_rooms);
        var listButtons = new HBoxContainer();
        listButtons.AddThemeConstantOverride("separation", 10);
        listButtons.AddChild(UiTheme.Button("Aktualisieren", Refresh, 150));
        listButtons.AddChild(UiTheme.Button("Beitreten", JoinSelected, 150));
        joinColumn.AddChild(listButtons);
        var codeRow = new HBoxContainer();
        codeRow.AddThemeConstantOverride("separation", 10);
        _code = new LineEdit { PlaceholderText = "Code, z. B. KXPM", MaxLength = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
        _code.TextSubmitted += _ => JoinByCode();
        codeRow.AddChild(_code);
        codeRow.AddChild(UiTheme.Button("Mit Code beitreten", JoinByCode, 190));
        joinColumn.AddChild(codeRow);
        join.AddChild(joinColumn);
        row.AddChild(join);

        column.AddChild(UiTheme.Button("Zurück zum Hauptmenü", () => _main.ShowMenu()));

        _client.Welcomed += OnWelcomed;
        _client.RoomListReceived += OnRoomList;
        _client.Failed += OnFailed;
    }

    public override void _ExitTree()
    {
        _client.Welcomed -= OnWelcomed;
        _client.RoomListReceived -= OnRoomList;
        _client.Failed -= OnFailed;
    }

    private void OnWelcomed()
    {
        _welcomed = true;
        _status.Text = $"Verbunden mit {_serverLabel} als {UserPrefs.PlayerName}.";
        Refresh();
    }

    private void OnFailed(string message) => _status.Text = message;

    private void OnRoomList(List<RoomListEntry> rooms)
    {
        _entries = rooms;
        _rooms.Clear();
        foreach (var room in rooms) _rooms.AddItem($"{room.Code}   {room.Host}   ({room.TimeControl})");
        if (rooms.Count == 0) _rooms.AddItem("Keine offenen Partien. Erstelle eine!");
    }

    private void Refresh()
    {
        if (_welcomed) _client.RequestRoomList();
    }

    private void CreateRoom()
    {
        if (!_welcomed) return;
        var (_, initial, increment) = TimeControls[_time.Selected];
        _client.CreateRoom(new RoomSettings
        {
            PlayerName = UserPrefs.PlayerName,
            HumanColor = _color.Selected switch { 1 => "Black", 2 => "Random", _ => "White" },
            InitialMs = initial,
            IncrementMs = increment,
            Public = true,
        });
    }

    private void JoinSelected()
    {
        var selected = _rooms.GetSelectedItems();
        if (!_welcomed || selected.Length == 0 || selected[0] >= _entries.Count) return;
        _client.JoinRoom(_entries[selected[0]].Code);
    }

    private void JoinByCode()
    {
        if (_welcomed && _code.Text.Trim().Length == 4) _client.JoinRoom(_code.Text.Trim());
    }
}
