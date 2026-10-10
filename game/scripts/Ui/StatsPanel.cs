using FpsChess.App;
using Godot;

namespace FpsChess.Ui;

/// <summary>The player's FPS Chess record across all games on this computer.</summary>
public partial class StatsPanel : Control
{
    private Label _text = null!;
    private Button _reset = null!;
    private bool _confirming;

    public event Action? Closed;

    public override void _Ready()
    {
        Name = "Stats";
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        column.AddChild(UiTheme.Label("Deine Statistik", 30, UiTheme.Accent, bold: true));
        _text = UiTheme.Label("", 18);
        column.AddChild(_text);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        _reset = UiTheme.Button("Zurücksetzen", Reset, 200);
        buttons.AddChild(_reset);
        buttons.AddChild(UiTheme.Button("Schließen", Close, 160));
        column.AddChild(buttons);
        panel.AddChild(column);
        center.AddChild(panel);
    }

    public void Open()
    {
        _confirming = false;
        _reset.Text = "Zurücksetzen";
        Refresh();
        Visible = true;
    }

    private void Refresh()
    {
        var s = UserPrefs.Career;
        if (s.Duels == 0 && s.Games == 0)
        {
            _text.Text = "Noch keine FPS-Chess-Partien. Spiel eine Partie oder ein Duell-Training!";
            return;
        }
        var favourite = s.FavouritePiece is { } f ? Texts.Piece(f) : "noch keine";
        var best = s.BestPiece is { } b ? Texts.Piece(b) : "ab 3 Duellen mit einer Figur";
        _text.Text = $"Partien: {s.Games} ({s.GamesWon} gewonnen, {s.GamesDrawn} remis, {s.GamesLost} verloren)\n" +
                     $"Duelle: {s.Duels} ({s.DuelsWon} gewonnen, Quote {s.DuelWinRate:P0})\n" +
                     $"Kopftreffer: {s.Headshots}\n" +
                     $"Schaden ausgeteilt: {s.DamageDealt:0}\n" +
                     $"Lieblingsfigur: {favourite}\n" +
                     $"Beste Figur: {best}";
    }

    private void Reset()
    {
        if (!_confirming)
        {
            _confirming = true;
            _reset.Text = "Wirklich zurücksetzen?";
            return;
        }
        UserPrefs.Career = new Chess.Core.Stats.CareerStats();
        _confirming = false;
        _reset.Text = "Zurücksetzen";
        Refresh();
    }

    private void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
