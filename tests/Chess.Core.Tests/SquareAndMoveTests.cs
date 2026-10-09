using Chess.Core;

namespace Chess.Core.Tests;

public class SquareAndMoveTests
{
    [Theory]
    [InlineData("a1", 0, 0)]
    [InlineData("h8", 7, 7)]
    [InlineData("e4", 4, 3)]
    public void SquareParsesAlgebraic(string text, int file, int rank)
    {
        var square = Square.Parse(text);

        Assert.Equal(new Square(file, rank), square);
        Assert.Equal(text, square.ToString());
    }

    [Theory]
    [InlineData("i1")]
    [InlineData("a9")]
    [InlineData("e")]
    public void InvalidSquareIsRejected(string text)
    {
        Assert.Throws<FormatException>(() => Square.Parse(text));
    }

    [Fact]
    public void MoveUciRoundTripsIncludingPromotion()
    {
        Assert.Equal("e7e8q", Move.ParseUci("e7e8q").ToUci());
        Assert.Equal(PieceType.Queen, Move.ParseUci("e7e8q").Promotion);
        Assert.Equal("g1f3", Move.ParseUci("g1f3").ToUci());
    }
}
