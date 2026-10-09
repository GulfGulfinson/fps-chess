using FpsChess.Ui;
using Godot;

namespace FpsChess.Ui;

public partial class PauseMenu : Control
{
    private Button _undo = null!;
    private Button _save = null!;
    private Button _resign = null!;
    private Label _toast = null!;

    public event Action? ResumeRequested;
    public event Action? UndoRequested;
    public event Action? SaveRequested;
    public event Action? ResignRequested;
    public event Action? MenuRequested;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        var title = UiTheme.Label("Pause", 32, UiTheme.Accent, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);

        _undo = UiTheme.Button("Zug zurücknehmen", () => UndoRequested?.Invoke());
        _resign = UiTheme.Button("Aufgeben", () => ResignRequested?.Invoke());
        column.AddChild(UiTheme.Button("Weiterspielen", () => ResumeRequested?.Invoke()));
        column.AddChild(_undo);
        _save = UiTheme.Button("Spiel speichern", () => SaveRequested?.Invoke());
        column.AddChild(_save);
        column.AddChild(_resign);
        column.AddChild(UiTheme.Button("Hauptmenü", () => MenuRequested?.Invoke()));

        _toast = UiTheme.Label("", 15, UiTheme.Accent);
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_toast);

        panel.AddChild(column);
        center.AddChild(panel);
    }

    public void Open(bool canUndo, bool canResign)
    {
        _undo.Disabled = !canUndo;
        _resign.Disabled = !canResign;
        _toast.Text = "";
        Visible = true;
    }

    public void ShowToast(string text) => _toast.Text = text;

    public void SetSaveEnabled(bool enabled) => _save.Disabled = !enabled;

    /// <summary>Hides the undo button for modes without undo.</summary>
    public void SetUndoVisible(bool visible) => _undo.Visible = visible;
}
