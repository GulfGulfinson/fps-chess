namespace Chess.Core;

public enum PieceColor
{
    White,
    Black,
}

public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King,
}

public readonly record struct Piece(PieceType Type, PieceColor Color)
{
    /// <summary>FEN letter: upper case for white, lower case for black.</summary>
    public char ToFenChar()
    {
        var c = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            _ => 'k',
        };
        return Color == PieceColor.White ? char.ToUpperInvariant(c) : c;
    }

    public static Piece? FromFenChar(char c)
    {
        PieceType? type = char.ToLowerInvariant(c) switch
        {
            'p' => PieceType.Pawn,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => null,
        };
        if (type is null) return null;
        return new Piece(type.Value, char.IsUpper(c) ? PieceColor.White : PieceColor.Black);
    }
}

public static class PieceColorExtensions
{
    public static PieceColor Opponent(this PieceColor color) =>
        color == PieceColor.White ? PieceColor.Black : PieceColor.White;
}
