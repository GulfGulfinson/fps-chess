using Chess.Core;
using Chess.Core.Session;
using FpsChess.Audio;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Board2D;

/// <summary>The classic board: draws the position and turns two clicks (piece, target) into a move.</summary>
public partial class BoardView2D : Control
{
    private const float AnimationSeconds = 0.18f;
    private static readonly Color Frame = new("3b2a1e");
    private static readonly Color LastMoveTint = new(1f, 0.85f, 0.2f, 0.38f);
    private static readonly Color SelectedTint = new(0.35f, 0.75f, 0.35f, 0.55f);
    private static readonly Color HintColor = new(0.1f, 0.1f, 0.1f, 0.28f);

    private GameSession _session = null!;
    private Square? _selected;
    private List<Move> _targets = [];
    private Square? _hovered;

    private PlayedMove? _animating;
    private float _animationProgress = 1f;

    public bool Flipped { get; set; }

    /// <summary>Raised when the clicked target is a promotion; the screen asks for the piece and calls <see cref="Play"/>.</summary>
    public event Action<List<Move>>? PromotionNeeded;

    public void Init(GameSession session)
    {
        _session = session;
        _session.MovePlayed += OnMovePlayed;
        _session.PositionReset += ClearSelection;
        _session.GameEnded += ClearSelection;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
    }

    public override void _ExitTree()
    {
        _session.MovePlayed -= OnMovePlayed;
        _session.PositionReset -= ClearSelection;
        _session.GameEnded -= ClearSelection;
    }

    public void Play(Move move)
    {
        ClearSelection();
        _session.TryPlayHumanMove(move);
    }

    public void ClearSelection()
    {
        _selected = null;
        _targets = [];
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_animationProgress < 1f)
        {
            _animationProgress = Mathf.Min(1f, _animationProgress + (float)delta / AnimationSeconds);
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (SquareAt(click.Position) is { } square) HandleClick(square);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                ClearSelection();
                break;
            case InputEventMouseMotion motion:
                var hovered = SquareAt(motion.Position);
                if (hovered != _hovered)
                {
                    _hovered = hovered;
                    MouseDefaultCursorShape = hovered is { } h && IsClickable(h) ? CursorShape.PointingHand : CursorShape.Arrow;
                    QueueRedraw();
                }
                break;
        }
    }

    private bool IsClickable(Square square) =>
        _session.IsHumanTurn &&
        (_targets.Any(m => m.To == square) || _session.Game.Position.PieceAt(square)?.Color == _session.Turn);

    private void HandleClick(Square square)
    {
        if (!_session.IsHumanTurn) return;

        var moves = _targets.Where(m => m.To == square).ToList();
        if (moves.Count > 1)
        {
            PromotionNeeded?.Invoke(moves);
            return;
        }
        if (moves.Count == 1)
        {
            Play(moves[0]);
            return;
        }

        if (_session.Game.Position.PieceAt(square)?.Color == _session.Turn && square != _selected)
        {
            _selected = square;
            _targets = _session.Game.LegalMovesFrom(square).ToList();
        }
        else
        {
            _selected = null;
            _targets = [];
        }
        QueueRedraw();
    }

    private void OnMovePlayed(PlayedMove played)
    {
        Sfx.Play(this, played.Captured is null ? Sound.PieceMove : Sound.Capture);
        _animating = played;
        _animationProgress = 0f;
        QueueRedraw();
    }

    private (Vector2 Origin, float Cell) Layout()
    {
        var side = Mathf.Min(Size.X, Size.Y) - 64f;
        return ((Size - new Vector2(side, side)) / 2f, side / 8f);
    }

    private Vector2 TopLeft(Square square)
    {
        var (origin, cell) = Layout();
        var column = Flipped ? 7 - square.File : square.File;
        var row = Flipped ? square.Rank : 7 - square.Rank;
        return origin + new Vector2(column, row) * cell;
    }

    private Square? SquareAt(Vector2 point)
    {
        var (origin, cell) = Layout();
        var local = (point - origin) / cell;
        if (local.X < 0 || local.Y < 0 || local.X >= 8 || local.Y >= 8) return null;
        var column = (int)local.X;
        var row = (int)local.Y;
        return Flipped ? new Square(7 - column, row) : new Square(column, 7 - row);
    }

    public override void _Draw()
    {
        var (origin, cell) = Layout();
        if (cell < 8f) return; // Not laid out yet.
        var position = _session.Game.Position;

        DrawRect(new Rect2(origin - new Vector2(14, 14), new Vector2(cell * 8 + 28, cell * 8 + 28)), Frame);

        for (var index = 0; index < 64; index++)
        {
            var square = Square.FromIndex(index);
            var rect = new Rect2(TopLeft(square), new Vector2(cell, cell));
            DrawRect(rect, square.IsLight ? UiTheme.LightSquare : UiTheme.DarkSquare);
            if (_session.Game.LastMove is { } last && (last.From == square || last.To == square)) DrawRect(rect, LastMoveTint);
            if (square == _selected) DrawRect(rect, SelectedTint);
            if (square == _hovered && IsClickable(square)) DrawRect(rect, new Color(1, 1, 1, 0.12f));
        }

        if (!_session.Game.IsOver || _session.Game.Status == GameStatus.Checkmate)
        {
            var side = position.SideToMove;
            if (position.IsInCheck(side))
            {
                var king = TopLeft(position.KingSquare(side)) + new Vector2(cell, cell) / 2f;
                for (var r = 1f; r > 0.2f; r -= 0.1f) DrawCircle(king, cell * 0.5f * r, new Color(0.9f, 0.1f, 0.1f, 0.12f));
            }
        }

        DrawCoordinates(cell);

        var animTarget = _animationProgress < 1f ? _animating?.Move.To : null;
        foreach (var (square, piece) in position.Pieces())
        {
            if (square == animTarget) continue;
            DrawPiece(piece, TopLeft(square), cell);
        }

        foreach (var move in _targets.DistinctBy(m => m.To))
        {
            var center = TopLeft(move.To) + new Vector2(cell, cell) / 2f;
            if (position.IsCapture(move)) DrawArc(center, cell * 0.43f, 0, Mathf.Tau, 48, HintColor, cell * 0.07f, true);
            else DrawCircle(center, cell * 0.16f, HintColor);
        }

        if (_animating is { } anim && _animationProgress < 1f)
        {
            var t = 1f - Mathf.Pow(1f - _animationProgress, 3f);
            var at = TopLeft(anim.Move.From).Lerp(TopLeft(anim.Move.To), t);
            var shown = anim.Move.Promotion is { } promotion ? new Piece(promotion, anim.Mover.Color) : anim.Mover;
            DrawPiece(shown, at, cell);
        }
    }

    private void DrawCoordinates(float cell)
    {
        var size = (int)(cell * 0.2f);
        for (var i = 0; i < 8; i++)
        {
            var fileSquare = new Square(i, Flipped ? 7 : 0);
            var fileColor = fileSquare.IsLight ? UiTheme.DarkSquare : UiTheme.LightSquare;
            DrawString(UiTheme.Bold, TopLeft(fileSquare) + new Vector2(cell - size * 0.8f, cell - size * 0.3f),
                ((char)('a' + i)).ToString(), HorizontalAlignment.Left, -1, size, fileColor);

            var rankSquare = new Square(Flipped ? 7 : 0, i);
            var rankColor = rankSquare.IsLight ? UiTheme.DarkSquare : UiTheme.LightSquare;
            DrawString(UiTheme.Bold, TopLeft(rankSquare) + new Vector2(size * 0.25f, size * 1.05f),
                (i + 1).ToString(), HorizontalAlignment.Left, -1, size, rankColor);
        }
    }

    private void DrawPiece(Piece piece, Vector2 topLeft, float cell)
    {
        var font = UiTheme.Font;
        var size = (int)(cell * 0.82f);
        var glyph = Texts.Glyph(piece.Type);
        var width = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, size).X;
        var baseline = topLeft + new Vector2((cell - width) / 2f, (cell - font.GetHeight(size)) / 2f + font.GetAscent(size));

        var white = piece.Color == PieceColor.White;
        var fill = white ? new Color("fbf8f1") : new Color("1c1c1f");
        var outline = white ? new Color("2b2622") : new Color("d8d2c4");
        DrawStringOutline(font, baseline, glyph, HorizontalAlignment.Left, -1, size, (int)(cell * (white ? 0.07f : 0.035f)), outline);
        DrawString(font, baseline, glyph, HorizontalAlignment.Left, -1, size, fill);
    }
}
