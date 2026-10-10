namespace Chess.Core.Duels;

/// <summary>
/// A position for practising one matchup: White's piece on e4 can capture Black's piece straight away.
/// Both kings stand out of the way unless one of the fighters is a king itself.
/// </summary>
public static class TrainingDuel
{
    private static readonly Square AttackerSquare = Square.Parse("e4");

    public static (string Fen, Move Capture) Create(PieceType attacker, PieceType defender)
    {
        var target = Square.Parse(attacker switch
        {
            PieceType.Pawn => "d5",
            PieceType.Knight => "d6",
            PieceType.Bishop => "c6",
            PieceType.King => "e5",
            _ => "e6",
        });

        var board = new Piece?[64];
        board[AttackerSquare.Index] = new Piece(attacker, PieceColor.White);
        board[target.Index] = new Piece(defender, PieceColor.Black);
        if (attacker != PieceType.King) board[Square.Parse("a1").Index] = new Piece(PieceType.King, PieceColor.White);
        if (defender != PieceType.King) board[Square.Parse("h8").Index] = new Piece(PieceType.King, PieceColor.Black);
        // One pawn each, far from the fight, so pairings like king against king are not an instant draw.
        board[Square.Parse("a2").Index] = new Piece(PieceType.Pawn, PieceColor.White);
        board[Square.Parse("h7").Index] = new Piece(PieceType.Pawn, PieceColor.Black);

        var rows = new List<string>();
        for (var rank = 7; rank >= 0; rank--)
        {
            var row = "";
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                if (board[new Square(file, rank).Index] is { } piece)
                {
                    if (empty > 0) row += empty;
                    empty = 0;
                    row += piece.ToFenChar();
                }
                else
                {
                    empty++;
                }
            }
            if (empty > 0) row += empty;
            rows.Add(row);
        }
        return ($"{string.Join('/', rows)} w - - 0 1", new Move(AttackerSquare, target));
    }
}
