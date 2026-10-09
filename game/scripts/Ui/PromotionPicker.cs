using Chess.Core;
using Godot;

namespace FpsChess.Ui;

/// <summary>Modal overlay asking which piece a pawn promotes to. Used by both views.</summary>
public partial class PromotionPicker : Control
{
    private static readonly PieceType[] Choices = [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    private HBoxContainer _buttons = null!;

    public event Action<PieceType>? Picked;
    public event Action? Cancelled;

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

        var panel = new PanelContainer();
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        var title = UiTheme.Label("Bauernumwandlung: Figur wählen", 22, UiTheme.Accent, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        _buttons = new HBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 12);
        column.AddChild(_buttons);
        column.AddChild(UiTheme.Button("Abbrechen", Cancel));
        panel.AddChild(column);
        center.AddChild(panel);
    }

    public void Open(PieceColor color)
    {
        foreach (var child in _buttons.GetChildren()) child.QueueFree();
        foreach (var type in Choices)
        {
            var button = UiTheme.Button($"{Texts.Glyph(type)}\n{Texts.Piece(type)}", () => Choose(type), 110);
            button.CustomMinimumSize = new Vector2(110, 120);
            button.AddThemeFontSizeOverride("font_size", 26);
            if (color == PieceColor.Black) button.AddThemeColorOverride("font_color", new Color("8f96a3"));
            _buttons.AddChild(button);
        }
        Visible = true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) Cancel();
    }

    private void Choose(PieceType type)
    {
        Visible = false;
        Picked?.Invoke(type);
    }

    private void Cancel()
    {
        Visible = false;
        Cancelled?.Invoke();
    }
}
