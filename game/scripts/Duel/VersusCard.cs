using Chess.Core.Duels;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Duel;

/// <summary>Shown before a duel: the player's fighter on the left, the opponent's on the right.</summary>
public partial class VersusCard : Control
{
    private HBoxContainer _row = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        _row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _row.AddThemeConstantOverride("separation", 28);
        center.AddChild(_row);
    }

    public void Open(FighterClass mine, FighterClass theirs, string opponentName, bool iAttack)
    {
        foreach (var child in _row.GetChildren()) child.QueueFree();
        _row.AddChild(Side(mine, iAttack ? "DU GREIFST AN" : "DU VERTEIDIGST", new Color("6bd1ff")));
        var versus = UiTheme.Label("VS", 64, UiTheme.Accent, bold: true);
        versus.VerticalAlignment = VerticalAlignment.Center;
        versus.AddThemeConstantOverride("outline_size", 10);
        versus.AddThemeColorOverride("font_outline_color", Colors.Black);
        _row.AddChild(versus);
        _row.AddChild(Side(theirs, opponentName.ToUpperInvariant(), new Color("ff6a4f")));

        Visible = true;
        Modulate = Colors.Transparent;
        Scale = new Vector2(0.96f, 0.96f);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "modulate", Colors.White, 0.25f);
        tween.TweenProperty(this, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private static PanelContainer Side(FighterClass fighter, string heading, Color color)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0), MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.08f, 0.09f, 0.11f, 0.92f), color, 3, 14, 22));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);

        column.AddChild(UiTheme.Label(heading, 18, color, bold: true));
        var glyph = UiTheme.Label(Texts.Glyph(fighter.Piece), 96, Colors.White);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(glyph);
        var name = UiTheme.Label($"{Texts.Piece(fighter.Piece)}: {fighter.Name}", 26, bold: true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(name);
        column.AddChild(Row("Leben", $"{fighter.MaxHealth:0}"));
        column.AddChild(Row("Waffe", fighter.Weapon.Name));
        column.AddChild(Row("Schaden", $"{fighter.Weapon.Damage * fighter.Weapon.Pellets / fighter.Weapon.FireInterval:0} pro Sekunde"));
        column.AddChild(Row("Fähigkeit", Texts.Perk(fighter)));
        panel.AddChild(column);
        return panel;
    }

    private static Control Row(string label, string value)
    {
        var row = new HBoxContainer();
        var key = UiTheme.Label(label, 17, UiTheme.Muted);
        key.CustomMinimumSize = new Vector2(100, 0);
        var text = UiTheme.Label(value, 17);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(key);
        row.AddChild(text);
        return row;
    }
}
