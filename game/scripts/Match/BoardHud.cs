using FpsChess.Ui;
using Godot;

namespace FpsChess.Match;

/// <summary>Board-phase overlay: status, players and clocks, recent moves, hints, banner and toasts.</summary>
public partial class BoardHud : Control
{
    private Label _status = null!;
    private Label _players = null!;
    private Label _moves = null!;
    private Label _hint = null!;
    private Label _banner = null!;
    private Label _toast = null!;
    private Label _aim = null!;
    private Tween? _bannerTween;
    private Tween? _toastTween;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        var left = Panel();
        var leftColumn = new VBoxContainer();
        _status = UiTheme.Label("", 22, bold: true);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(460, 0);
        _players = UiTheme.Label("", 18);
        leftColumn.AddChild(_status);
        leftColumn.AddChild(_players);
        left.AddChild(leftColumn);
        left.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.KeepSize, 24);

        var right = Panel();
        var rightColumn = new VBoxContainer();
        rightColumn.AddChild(UiTheme.Label("Züge", 15, UiTheme.Muted));
        _moves = UiTheme.Label("", 17);
        _moves.CustomMinimumSize = new Vector2(230, 0);
        rightColumn.AddChild(_moves);
        right.AddChild(rightColumn);
        right.GrowHorizontal = GrowDirection.Begin;
        right.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.KeepSize, 24);

        _hint = Outlined(UiTheme.Label("Linksklick: Figur wählen und ziehen     Rechte Maustaste halten: Kamera drehen     Mausrad: Zoom     1-4: Kurznachrichten (online)     Esc: Menü", 15, UiTheme.Muted));
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_hint);
        _hint.GrowHorizontal = GrowDirection.Both;
        _hint.GrowVertical = GrowDirection.Begin;
        _hint.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.KeepSize, 20);

        _aim = Outlined(UiTheme.Label("", 20, bold: true));
        _aim.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_aim);
        _aim.GrowHorizontal = GrowDirection.Both;
        _aim.GrowVertical = GrowDirection.Begin;
        _aim.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.KeepSize, 92);

        _toast = Outlined(UiTheme.Label("", 18, UiTheme.Accent));
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_toast);
        _toast.GrowHorizontal = GrowDirection.Both;
        _toast.GrowVertical = GrowDirection.Begin;
        _toast.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.KeepSize, 56);

        _banner = Outlined(UiTheme.Label("", 54, UiTheme.Accent, bold: true));
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.Modulate = Colors.Transparent;
        AddChild(_banner);
        _banner.GrowHorizontal = GrowDirection.Both;
        _banner.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.KeepSize, 130);
    }

    private PanelContainer Panel()
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.08f, 0.09f, 0.11f, 0.78f), padding: 14));
        AddChild(panel);
        return panel;
    }

    private static Label Outlined(Label label)
    {
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        return label;
    }

    public void SetStatus(string text) => _status.Text = text;

    /// <summary>What the mouse points at on the board: a piece's class or a capture's duel odds.</summary>
    public void SetAimInfo(string text, Color color)
    {
        _aim.Text = text;
        _aim.AddThemeColorOverride("font_color", color);
    }
    public void SetPlayers(string text) => _players.Text = text;
    public void SetMoves(string text) => _moves.Text = text;

    public void ShowBanner(string text, Color color)
    {
        _banner.Text = text;
        _banner.AddThemeColorOverride("font_color", color);
        _bannerTween?.Kill();
        _banner.Modulate = Colors.White;
        _bannerTween = CreateTween();
        _bannerTween.TweenInterval(1.4);
        _bannerTween.TweenProperty(_banner, "modulate:a", 0f, 0.6f);
    }

    public void ShowToast(string text)
    {
        _toast.Text = text;
        _toastTween?.Kill();
        _toast.Modulate = Colors.White;
        _toastTween = CreateTween();
        _toastTween.TweenInterval(4);
        _toastTween.TweenProperty(_toast, "modulate:a", 0f, 0.8f);
    }
}
