using FpsChess.Ui;
using Godot;

namespace FpsChess.Fps;

public enum CrosshairMode
{
    Idle,
    Select,
    Move,
    Capture,
}

/// <summary>Heads-up display: crosshair, status, clocks, recent moves, aim info and a big centered banner.</summary>
public partial class FpsHud : Control
{
    private Label _status = null!;
    private Label _clocks = null!;
    private Label _moves = null!;
    private Label _aimInfo = null!;
    private Label _banner = null!;
    private Crosshair _crosshair = null!;
    private Tween? _bannerTween;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        _crosshair = new Crosshair();
        AddChild(_crosshair);

        var topLeft = Panel(new Vector2(24, 24));
        var left = new VBoxContainer();
        _status = UiTheme.Label("", 22, bold: true);
        _clocks = UiTheme.Label("", 20);
        left.AddChild(_status);
        left.AddChild(_clocks);
        topLeft.AddChild(left);

        var topRight = Panel(Vector2.Zero);
        topRight.GrowHorizontal = GrowDirection.Begin;
        topRight.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.KeepSize, 24);
        var right = new VBoxContainer();
        right.AddChild(UiTheme.Label("Letzte Züge", 15, UiTheme.Muted));
        _moves = UiTheme.Label("", 17);
        _moves.CustomMinimumSize = new Vector2(220, 0);
        right.AddChild(_moves);
        topRight.AddChild(right);

        var bottom = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bottom.GrowHorizontal = GrowDirection.Both;
        bottom.GrowVertical = GrowDirection.Begin;
        _aimInfo = UiTheme.Label("", 22, bold: true);
        _aimInfo.HorizontalAlignment = HorizontalAlignment.Center;
        _aimInfo.AddThemeConstantOverride("outline_size", 6);
        _aimInfo.AddThemeColorOverride("font_outline_color", Colors.Black);
        var hint = UiTheme.Label("Linksklick: Figur wählen / ziehen     Rechtsklick: abwählen     WASD + Maus: laufen     Shift: rennen     Esc: Menü", 15, UiTheme.Muted);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeConstantOverride("outline_size", 4);
        hint.AddThemeColorOverride("font_outline_color", Colors.Black);
        bottom.AddChild(_aimInfo);
        bottom.AddChild(hint);
        AddChild(bottom);
        bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.KeepSize, 24);

        _banner = UiTheme.Label("", 54, UiTheme.Accent, bold: true);
        _banner.GrowHorizontal = GrowDirection.Both;
        _banner.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.KeepSize, 140);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.AddThemeConstantOverride("outline_size", 10);
        _banner.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _banner.Modulate = Colors.Transparent;
        AddChild(_banner);
    }

    private PanelContainer Panel(Vector2 position)
    {
        var panel = new PanelContainer { Position = position, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.08f, 0.09f, 0.11f, 0.75f), padding: 14));
        AddChild(panel);
        return panel;
    }

    public void SetStatus(string text) => _status.Text = text;
    public void SetClocks(string text) => _clocks.Text = text;
    public void SetMoves(string text) => _moves.Text = text;
    public void SetAimInfo(string text, Color color)
    {
        _aimInfo.Text = text;
        _aimInfo.AddThemeColorOverride("font_color", color);
    }

    public void SetCrosshair(CrosshairMode mode) => _crosshair.Mode = mode;

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

    private sealed partial class Crosshair : Control
    {
        private CrosshairMode _mode;

        public CrosshairMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                QueueRedraw();
            }
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            var center = Size / 2f;
            var color = Mode switch
            {
                CrosshairMode.Select => new Color("ffd36b"),
                CrosshairMode.Move => new Color("6bff8f"),
                CrosshairMode.Capture => new Color("ff5a4f"),
                _ => new Color(1, 1, 1, 0.8f),
            };
            var gap = Mode == CrosshairMode.Idle ? 5f : 9f;
            const float length = 9f;
            foreach (var direction in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
            {
                DrawLine(center + direction * gap, center + direction * (gap + length), Colors.Black, 4f);
                DrawLine(center + direction * gap, center + direction * (gap + length), color, 2f);
            }
            DrawCircle(center, 2f, color);
            if (Mode == CrosshairMode.Capture) DrawArc(center, 18f, 0, Mathf.Tau, 32, color, 2f, true);
        }
    }
}
