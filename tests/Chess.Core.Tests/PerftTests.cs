using Chess.Core;

namespace Chess.Core.Tests;

// Reference node counts from https://www.chessprogramming.org/Perft_Results.
// Matching them proves the legal move generator, including castling, en passant and promotion.
public class PerftTests
{
    private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    private const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    private const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    private const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";

    [Theory]
    [InlineData(Position.StartFen, 1, 20)]
    [InlineData(Position.StartFen, 2, 400)]
    [InlineData(Position.StartFen, 3, 8902)]
    [InlineData(Position.StartFen, 4, 197281)]
    [InlineData(Kiwipete, 1, 48)]
    [InlineData(Kiwipete, 2, 2039)]
    [InlineData(Kiwipete, 3, 97862)]
    [InlineData(Position3, 1, 14)]
    [InlineData(Position3, 2, 191)]
    [InlineData(Position3, 3, 2812)]
    [InlineData(Position3, 4, 43238)]
    [InlineData(Position4, 1, 6)]
    [InlineData(Position4, 2, 264)]
    [InlineData(Position4, 3, 9467)]
    [InlineData(Position5, 1, 44)]
    [InlineData(Position5, 2, 1486)]
    [InlineData(Position5, 3, 62379)]
    public void NodeCountsMatchReference(string fen, int depth, long expected)
    {
        Assert.Equal(expected, Perft(Position.FromFen(fen), depth));
    }

    private static long Perft(Position position, int depth)
    {
        var moves = position.GenerateLegalMoves();
        if (depth == 1) return moves.Count;

        long nodes = 0;
        foreach (var move in moves) nodes += Perft(position.Apply(move), depth - 1);
        return nodes;
    }
}
