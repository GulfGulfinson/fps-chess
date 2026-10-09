using Chess.Core;

namespace Chess.Core.Tests;

public class NotationTests
{
    [Theory]
    [InlineData(Position.StartFen, "e2e4", "e4")]
    [InlineData(Position.StartFen, "g1f3", "Nf3")]
    [InlineData("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1c1", "O-O-O")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2", "e5d6", "exd6")]
    [InlineData("7k/P7/8/8/8/8/8/K7 w - - 0 1", "a7a8q", "a8=Q+")]
    [InlineData("4k3/8/8/8/8/8/6K1/R6R w - - 0 1", "a1d1", "Rad1")]
    [InlineData("4k3/8/8/8/R7/8/8/R3K3 w - - 0 1", "a1a3", "R1a3")]
    [InlineData("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", "a1a8", "Ra8#")]
    public void FormatsStandardAlgebraicNotation(string fen, string uci, string expected)
    {
        Assert.Equal(expected, Notation.ToSan(Position.FromFen(fen), Move.ParseUci(uci)));
    }
}
