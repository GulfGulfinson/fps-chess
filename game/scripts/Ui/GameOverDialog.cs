using Godot;

namespace FpsChess.Ui;

/// <summary>Centered result banner with rematch / menu / close.</summary>
public partial class GameOverDialog : Control
{
    private Label _result = null!;
    private Button _rematch = null!;
    private Label _status = null!;

    public event Action? RematchRequested;
    public event Action? MenuRequested;
    public event Action? Closed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        var title = UiTheme.Label("Partie beendet", 30, UiTheme.Accent, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        _result = UiTheme.Label("", 20);
        _result.HorizontalAlignment = HorizontalAlignment.Center;
        _result.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_result);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        _rematch = UiTheme.Button("Revanche", () => RematchRequested?.Invoke(), 140);
        buttons.AddChild(_rematch);
        buttons.AddChild(UiTheme.Button("Hauptmenü", () => MenuRequested?.Invoke(), 140));
        buttons.AddChild(UiTheme.Button("Brett ansehen", Close, 160));
        column.AddChild(buttons);
        _status = UiTheme.Label("", 16, UiTheme.Accent);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_status);
        panel.AddChild(column);
        center.AddChild(panel);
    }

    public void SetRematchVisible(bool visible) => _rematch.Visible = visible;

    /// <summary>A line under the buttons, e.g. "Warte auf den Gegner".</summary>
    public void SetStatus(string text) => _status.Text = text;

    public void Open(string result)
    {
        _result.Text = result;
        _status.Text = "";
        Visible = true;
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
