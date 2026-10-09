namespace Chess.Core.Session;

/// <summary>
/// A move that was just played, with everything a view needs to animate it:
/// the captured piece and where it stood (differs from <see cref="Move.To"/> for en passant)
/// and the rook's path when castling.
/// </summary>
public sealed record PlayedMove(
    Move Move,
    string San,
    Piece Mover,
    Piece? Captured,
    Square? CapturedOn,
    (Square From, Square To)? RookMove)
{
    public static PlayedMove Describe(Position before, Move move, string san)
    {
        var mover = before.PieceAt(move.From) ?? throw new ArgumentException($"No piece on {move.From}.", nameof(move));

        Piece? captured = before.PieceAt(move.To);
        Square? capturedOn = captured is null ? null : move.To;
        if (captured is null && mover.Type == PieceType.Pawn && move.To == before.EnPassant)
        {
            capturedOn = new Square(move.To.File, move.From.Rank);
            captured = before.PieceAt(capturedOn.Value);
        }

        (Square, Square)? rookMove = null;
        if (mover.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2)
        {
            var kingSide = move.To.File == 6;
            rookMove = (new Square(kingSide ? 7 : 0, move.From.Rank), new Square(kingSide ? 5 : 3, move.From.Rank));
        }

        return new PlayedMove(move, san, mover, captured, capturedOn, rookMove);
    }
}
