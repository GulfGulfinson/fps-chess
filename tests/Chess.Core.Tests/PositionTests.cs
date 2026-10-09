using Chess.Core;

namespace Chess.Core.Tests;

public class PositionTests
{
    [Theory]
    [InlineData(Position.StartFen)]
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2")]
    [InlineData("8/8/8/8/8/8/8/K6k b - - 37 80")]
    public void FenRoundTrips(string fen)
    {
        Assert.Equal(fen, Position.FromFen(fen).ToFen());
    }

    [Theory]
    [InlineData("")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNX w KQkq - 0 1")]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1")]
    public void InvalidFenIsRejected(string fen)
    {
        Assert.Throws<FormatException>(() => Position.FromFen(fen));
    }

    [Fact]
    public void EnPassantCaptureRemovesThePassedPawn()
    {
        var position = Position.FromFen("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2");

        var after = position.Apply(Move.ParseUci("e5d6"));

        Assert.Null(after.PieceAt(Square.Parse("d5")));
        Assert.Equal(new Piece(PieceType.Pawn, PieceColor.White), after.PieceAt(Square.Parse("d6")));
    }

    [Fact]
    public void CastlingMovesTheRookAndClearsRights()
    {
        var position = Position.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        var after = position.Apply(Move.ParseUci("e1g1"));

        Assert.Equal(new Piece(PieceType.Rook, PieceColor.White), after.PieceAt(Square.Parse("f1")));
        Assert.Null(after.PieceAt(Square.Parse("h1")));
        Assert.Equal("r3k2r/8/8/8/8/8/8/R4RK1 b kq - 1 1", after.ToFen());
    }

    [Fact]
    public void CannotCastleThroughAttackedSquare()
    {
        // The black rook on f8 covers f1, so white may not castle king side.
        var position = Position.FromFen("4kr2/8/8/8/8/8/8/4K2R w K - 0 1");

        Assert.DoesNotContain(Move.ParseUci("e1g1"), position.GenerateLegalMoves());
    }

    [Fact]
    public void PinnedPieceCannotLeaveThePin()
    {
        var position = Position.FromFen("4r1k1/8/8/8/8/8/4B3/4K3 w - - 0 1");

        Assert.DoesNotContain(position.GenerateLegalMoves(), m => m.From == Square.Parse("e2"));
    }

    [Fact]
    public void PromotionOffersFourPieces()
    {
        var position = Position.FromFen("8/P6k/8/8/8/8/8/K7 w - - 0 1");

        var promotions = position.GenerateLegalMoves().Where(m => m.From == Square.Parse("a7")).ToList();

        Assert.Equal(4, promotions.Count);
        Assert.All(promotions, m => Assert.NotNull(m.Promotion));
    }
}
