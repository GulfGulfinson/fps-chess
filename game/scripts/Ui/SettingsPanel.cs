using FpsChess.App;
using Godot;

namespace FpsChess.Ui;

/// <summary>Modal settings: mouse, field of view, volume and graphics. Every change is applied and saved at once.</summary>
public partial class SettingsPanel : Control
{
    public event Action? Closed;

    /// <summary>Raised after mouse or field of view changed, so a running duel can pick the values up.</summary>
    public event Action? Changed;

    public override void _Ready()
    {
        Name = "Settings";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        column.AddChild(UiTheme.Label("Einstellungen", 30, UiTheme.Accent, bold: true));

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 14);
        SliderRow(grid, "Mausempfindlichkeit", 0.2, 3, 0.05, UserPrefs.MouseSensitivity, v => $"{v:0.00}x", v =>
        {
            UserPrefs.MouseSensitivity = (float)v;
            Changed?.Invoke();
        });
        SliderRow(grid, "Sichtfeld", 65, 100, 1, UserPrefs.FieldOfView, v => $"{v:0}°", v =>
        {
            UserPrefs.FieldOfView = (float)v;
            Changed?.Invoke();
        });
        SliderRow(grid, "Lautstärke", 0, 1, 0.05, UserPrefs.Volume, v => $"{v:P0}", v =>
        {
            UserPrefs.Volume = (float)v;
            UserPrefs.ApplyAudio();
        });

        SliderRow(grid, "Musik", 0, 1, 0.05, UserPrefs.MusicVolume, v => $"{v:P0}", v =>
        {
            UserPrefs.MusicVolume = (float)v;
            Changed?.Invoke();
        });

        grid.AddChild(UiTheme.Label("Ton aus", 18, UiTheme.Muted));
        var mute = new CheckButton { ButtonPressed = UserPrefs.Muted };
        mute.Toggled += on =>
        {
            UserPrefs.Muted = on;
            UserPrefs.ApplyAudio();
        };
        grid.AddChild(mute);
        grid.AddChild(new Control());

        grid.AddChild(UiTheme.Label("Farbenblind-Modus", 18, UiTheme.Muted));
        var colorblind = new CheckButton { ButtonPressed = UserPrefs.ColorblindMode };
        colorblind.Toggled += on =>
        {
            UserPrefs.ColorblindMode = on;
            Changed?.Invoke();
        };
        grid.AddChild(colorblind);
        grid.AddChild(UiTheme.Label("Blau/Orange statt Grün/Rot", 14, UiTheme.Muted));

        grid.AddChild(UiTheme.Label("Grafik", 18, UiTheme.Muted));
        var graphics = new OptionButton { CustomMinimumSize = new Vector2(300, 44) };
        foreach (var level in Enum.GetValues<GraphicsLevel>()) graphics.AddItem(GraphicsQuality.Label(level));
        graphics.Selected = (int)UserPrefs.Graphics;
        graphics.ItemSelected += index => UserPrefs.Graphics = (GraphicsLevel)(int)index;
        grid.AddChild(graphics);
        grid.AddChild(UiTheme.Label("ab der nächsten Partie", 14, UiTheme.Muted));

        column.AddChild(grid);
        column.AddChild(UiTheme.Button("Schließen", Close));
        panel.AddChild(column);
        center.AddChild(panel);
    }

    private static void SliderRow(GridContainer grid, string label, double min, double max, double step, double value,
        Func<double, string> format, Action<double> changed)
    {
        grid.AddChild(UiTheme.Label(label, 18, UiTheme.Muted));
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(300, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        var display = UiTheme.Label(format(value), 18);
        display.CustomMinimumSize = new Vector2(70, 0);
        slider.ValueChanged += v =>
        {
            display.Text = format(v);
            changed(v);
        };
        grid.AddChild(slider);
        grid.AddChild(display);
    }

    public void Open() => Visible = true;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape }) Close();
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
