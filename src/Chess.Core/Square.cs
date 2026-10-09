namespace Chess.Core;

/// <summary>A board square. File 0..7 is a..h, Rank 0..7 is 1..8.</summary>
public readonly record struct Square(int File, int Rank)
{
    public int Index => Rank * 8 + File;

    public bool IsOnBoard => File is >= 0 and < 8 && Rank is >= 0 and < 8;

    /// <summary>Light squares are those where file and rank have different parity (h1 is light).</summary>
    public bool IsLight => (File + Rank) % 2 == 1;

    public static Square FromIndex(int index) => new(index % 8, index / 8);

    public Square Offset(int fileDelta, int rankDelta) => new(File + fileDelta, Rank + rankDelta);

    public static Square Parse(string text)
    {
        if (text.Length != 2) throw new FormatException($"Invalid square '{text}'.");
        var square = new Square(text[0] - 'a', text[1] - '1');
        if (!square.IsOnBoard) throw new FormatException($"Invalid square '{text}'.");
        return square;
    }

    public override string ToString() => $"{(char)('a' + File)}{(char)('1' + Rank)}";
}
