namespace Chess.Core;

/// <summary>Standard algebraic notation (SAN), e.g. "Nf3", "exd6", "O-O", "a8=Q+".</summary>
public static class Notation
{
    public static string ToSan(Position position, Move move)
    {
        var piece = position.PieceAt(move.From) ?? throw new ArgumentException($"No piece on {move.From}.", nameof(move));
        var san = piece.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2
            ? (move.To.File == 6 ? "O-O" : "O-O-O")
            : MoveText(position, move, piece);

        var after = position.Apply(move);
        if (after.IsInCheck(after.SideToMove))
            san += after.GenerateLegalMoves().Count == 0 ? "#" : "+";
        return san;
    }

    private static string MoveText(Position position, Move move, Piece piece)
    {
        var capture = position.IsCapture(move) ? "x" : "";
        var promotion = move.Promotion is { } p ? "=" + Letter(p) : "";

        if (piece.Type == PieceType.Pawn)
        {
            var file = capture.Length > 0 ? ((char)('a' + move.From.File)).ToString() : "";
            return $"{file}{capture}{move.To}{promotion}";
        }

        return $"{Letter(piece.Type)}{Disambiguation(position, move, piece)}{capture}{move.To}";
    }

    private static string Disambiguation(Position position, Move move, Piece piece)
    {
        var rivals = position.GenerateLegalMoves()
            .Where(m => m.To == move.To && m.From != move.From && position.PieceAt(m.From) == piece)
            .Select(m => m.From)
            .ToList();
        if (rivals.Count == 0) return "";

        var fileChar = ((char)('a' + move.From.File)).ToString();
        if (rivals.All(r => r.File != move.From.File)) return fileChar;
        if (rivals.All(r => r.Rank != move.From.Rank)) return ((char)('1' + move.From.Rank)).ToString();
        return move.From.ToString();
    }

    public static string Letter(PieceType type) => type switch
    {
        PieceType.Knight => "N",
        PieceType.Bishop => "B",
        PieceType.Rook => "R",
        PieceType.Queen => "Q",
        PieceType.King => "K",
        _ => "",
    };
}
