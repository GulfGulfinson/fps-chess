using Chess.Core;
using Chess.Core.Ai;

namespace Chess.Core.Tests;

public class ChessAiTests
{
    private static Move Best(string fen, int depth) =>
        new ChessAi(depth, new Random(1)).ChooseMove(Position.FromFen(fen));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FindsMateInOne(int depth)
    {
        Assert.Equal("a1a8", Best("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", depth).ToUci());
    }

    [Fact]
    public void CapturesAHangingQueen()
    {
        Assert.Equal("d2d5", Best("4k3/8/8/3q4/8/8/3R4/4K3 w - - 0 1", 2).ToUci());
    }

    [Fact]
    public void DoesNotTakeADefendedPawnWithTheQueen()
    {
        // Qxd6 is met by exd6; quiescence search must see that even at depth 1.
        Assert.NotEqual("d1d6", Best("4k3/4p3/3p4/8/8/8/8/3QK3 w - - 0 1", 1).ToUci());
    }

    [Fact]
    public void FindsMateInTwo()
    {
        // 1. Rd8+ Rxd8 2. Rxd8#
        Assert.Equal("d2d8", Best("2r3k1/5ppp/8/8/8/8/3R1PPP/3R2K1 w - - 0 1", 3).ToUci());
    }

    [Fact]
    public void BlackAlsoPlaysWell()
    {
        Assert.Equal("d7d4", Best("4k3/3r4/8/8/3Q4/8/8/4K3 b - - 0 1", 2).ToUci());
    }

    [Fact]
    public void ReturnsALegalMoveFromTheStart()
    {
        var move = new ChessAi(3).ChooseMove(Position.Start);

        Assert.Contains(move, Position.Start.GenerateLegalMoves());
    }

    [Fact]
    public void ThrowsWhenThereIsNoLegalMove()
    {
        Assert.Throws<InvalidOperationException>(() => Best("k7/8/1Q6/8/8/8/8/7K b - - 0 1", 2));
    }

    [Fact]
    public void RejectsInvalidDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessAi(0));
    }
}
