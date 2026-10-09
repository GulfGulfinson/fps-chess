namespace Chess.Core;

/// <summary>
/// A move from one square to another. Castling is encoded as the king moving two files and
/// en passant as the pawn moving to the en passant square; <see cref="Position.Apply"/> resolves both.
/// </summary>
public readonly record struct Move(Square From, Square To, PieceType? Promotion = null)
{
    public string ToUci()
    {
        var promotion = Promotion switch
        {
            PieceType.Queen => "q",
            PieceType.Rook => "r",
            PieceType.Bishop => "b",
            PieceType.Knight => "n",
            _ => "",
        };
        return $"{From}{To}{promotion}";
    }

    public static Move ParseUci(string text)
    {
        if (text.Length is not (4 or 5)) throw new FormatException($"Invalid move '{text}'.");
        PieceType? promotion = null;
        if (text.Length == 5)
        {
            promotion = text[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => throw new FormatException($"Invalid promotion in move '{text}'."),
            };
        }
        return new Move(Square.Parse(text[..2]), Square.Parse(text[2..4]), promotion);
    }

    public override string ToString() => ToUci();
}
