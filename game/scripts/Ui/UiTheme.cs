using Godot;

namespace FpsChess.Ui;

/// <summary>Shared colours, fonts and the Godot theme for every screen.</summary>
public static class UiTheme
{
    public static readonly Color Background = new("14161c");
    public static readonly Color Panel = new("1e222b");
    public static readonly Color PanelLight = new("2a2f3a");
    public static readonly Color Accent = new("e0a84a");
    public static readonly Color Text = new("e8e6e3");
    public static readonly Color Muted = new("9aa0aa");
    public static readonly Color Danger = new("d9534f");

    /// <summary>"Good" and "bad" signal colours; blue and orange in colourblind mode instead of green and red.</summary>
    public static Color Good => App.UserPrefs.ColorblindMode ? new Color("5ab4ff") : new Color("7dff9b");
    public static Color Bad => App.UserPrefs.ColorblindMode ? new Color("ff9d2e") : new Color("ff6a5f");

    public static readonly Color LightSquare = new("ebd9b4");
    public static readonly Color DarkSquare = new("b07d52");

    private static Theme? _theme;
    private static FontFile? _font;
    private static FontFile? _bold;

    public static FontFile Font => _font ??= GD.Load<FontFile>("res://assets/fonts/DejaVuSans.ttf");
    public static FontFile Bold => _bold ??= GD.Load<FontFile>("res://assets/fonts/DejaVuSans-Bold.ttf");

    public static Theme Get() => _theme ??= Build();

    private static Theme Build()
    {
        var theme = new Theme { DefaultFont = Font, DefaultFontSize = 18 };

        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Box(PanelLight));
            theme.SetStylebox("hover", type, Box(PanelLight.Lightened(0.12f)));
            theme.SetStylebox("pressed", type, Box(Accent.Darkened(0.35f)));
            theme.SetStylebox("disabled", type, Box(PanelLight.Darkened(0.3f)));
            theme.SetStylebox("focus", type, Box(Colors.Transparent, Accent, 2));
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_hover_color", type, Colors.White);
            theme.SetColor("font_pressed_color", type, Colors.White);
            theme.SetColor("font_disabled_color", type, Muted.Darkened(0.3f));
        }

        theme.SetStylebox("panel", "PanelContainer", Box(Panel, Accent.Darkened(0.5f), 1, 14, 24));
        theme.SetStylebox("panel", "PopupMenu", Box(Panel, Accent.Darkened(0.5f), 1));
        theme.SetStylebox("panel", "ItemList", Box(Background, PanelLight, 1));
        theme.SetStylebox("normal", "RichTextLabel", Box(Background, PanelLight, 1));
        theme.SetColor("font_color", "Label", Text);
        theme.SetColor("font_color", "ItemList", Text);
        theme.SetColor("font_selected_color", "ItemList", Colors.White);
        theme.SetStylebox("selected", "ItemList", Box(Accent.Darkened(0.35f)));
        theme.SetStylebox("selected_focus", "ItemList", Box(Accent.Darkened(0.35f)));
        theme.SetColor("default_color", "RichTextLabel", Text);
        theme.SetColor("font_color", "CheckButton", Text);
        theme.SetColor("font_hover_color", "CheckButton", Colors.White);
        theme.SetColor("font_pressed_color", "CheckButton", Text);
        return theme;
    }

    public static StyleBoxFlat Box(Color background, Color? border = null, int borderWidth = 0, int radius = 8, int padding = 10)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border ?? Colors.Transparent,
            ContentMarginLeft = padding + 4,
            ContentMarginRight = padding + 4,
            ContentMarginTop = padding * 0.6f,
            ContentMarginBottom = padding * 0.6f,
        };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        return box;
    }

    public static Label Label(string text, int size = 18, Color? color = null, bool bold = false)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) label.AddThemeColorOverride("font_color", c);
        if (bold) label.AddThemeFontOverride("font", Bold);
        return label;
    }

    public static Button Button(string text, Action onPressed, int minWidth = 0)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 44) };
        button.Pressed += onPressed;
        return button;
    }
}
