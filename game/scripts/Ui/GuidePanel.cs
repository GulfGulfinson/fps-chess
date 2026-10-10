using Chess.Core;
using Chess.Core.Duels;
using Godot;

namespace FpsChess.Ui;

/// <summary>Rules, piece classes and controls of FPS Chess, generated from the same data the game uses.</summary>
public partial class GuidePanel : Control
{
    public event Action? Closed;

    public override void _Ready()
    {
        Name = "Guide";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(860, 640) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(UiTheme.Label("Anleitung: FPS Chess", 30, UiTheme.Accent, bold: true));

        var text = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Text = BuildText(),
        };
        text.AddThemeFontSizeOverride("normal_font_size", 17);
        text.AddThemeFontSizeOverride("bold_font_size", 17);
        text.AddThemeFontOverride("bold_font", UiTheme.Bold);
        column.AddChild(text);
        column.AddChild(UiTheme.Button("Schließen", Close));
        panel.AddChild(column);
        center.AddChild(panel);
    }

    private static string BuildText()
    {
        var classes = string.Join("\n", Enum.GetValues<PieceType>().Select(type =>
        {
            var fighter = FighterClasses.For(type);
            var weapon = fighter.Weapon;
            return $"[b]{Texts.Glyph(type)} {Texts.Piece(type)}[/b] ({fighter.Name}): {fighter.MaxHealth:0} Leben, {weapon.Name}, " +
                   $"{weapon.Damage * weapon.Pellets / weapon.FireInterval:0} Schaden pro Sekunde. Fähigkeit: {Texts.Perk(fighter)}.";
        }));

        return $$"""
            [b]Die Idee[/b]
            Gespielt wird normales Schach. Sobald eine Figur eine andere schlagen will, beginnt ein Duell: Die Kamera fliegt in deine beteiligte Figur, und ihr kämpft aus der Ego-Perspektive auf dem Brett. Die anderen Figuren sind Deckung, auf dem Tisch stehen Hindernisse.

            [b]Duellregeln[/b]
            • 3 Sekunden Countdown, dann 60 Sekunden Kampf.
            • Wer keine Lebenspunkte mehr hat oder vom Tisch fällt, verliert.
            • Läuft die Zeit ab, gewinnt die verteidigende Figur.
            • Gewinnt der Angreifer, wird geschlagen wie im Schach. Verliert er, wird er vom Brett genommen und die verteidigende Figur bleibt stehen.
            • Kopftreffer (oberer Teil der Figur) machen {{FighterClasses.HeadshotMultiplier.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}}-fachen Schaden.
            • Steht danach dein König im Angriff, darf der Gegner ihn schlagen. Verliert der König dieses Duell, ist die Partie vorbei.

            [b]Die Figuren[/b]
            {{classes}}

            [b]Steuerung auf dem Brett[/b]
            Linksklick: Figur wählen, dann Zielfeld (grün: Zug, rot: Schlagen). Rechte Maustaste halten: Kamera drehen. Mausrad: Zoom. Esc: Menü.

            [b]Steuerung im Duell[/b]
            W A S D: laufen. Maus: zielen. Linksklick (halten): schießen. Rechtsklick: zielen, beim Läufer Zielfernrohr. R: nachladen. Leertaste: springen (Springer zweimal). Shift: rennen, bei der Dame Dash. G: Granate (Dame). Esc: Menü.

            [b]Tipps[/b]
            • Rechtsklick halbiert die Streuung: auf Distanz immer zielen.
            • Der Turm muss seine Minigun anlaufen lassen, halte die Maustaste schon vor dem Gegner gedrückt.
            • Hinter Figuren und Hindernissen bist du sicher, auch zum Nachladen.
            • Läuft es ruckelig: Einstellungen, Grafik auf Niedrig.
            """;
    }

    public void Open() => Visible = true;

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
