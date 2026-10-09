using Chess.Core;

namespace Chess.Core.Tests;

public class GameTests
{
    private static Game Play(params string[] uciMoves) => PlayFrom(Position.StartFen, uciMoves);

    private static Game PlayFrom(string fen, params string[] uciMoves)
    {
        var game = new Game(fen);
        foreach (var uci in uciMoves) game.MakeMove(Move.ParseUci(uci));
        return game;
    }

    [Fact]
    public void NewGameIsOngoingWithWhiteToMove()
    {
        var game = new Game();

        Assert.Equal(GameStatus.Ongoing, game.Status);
        Assert.Equal(PieceColor.White, game.Position.SideToMove);
        Assert.Equal(20, game.LegalMoves.Count);
    }

    [Fact]
    public void IllegalMoveIsRejectedAndStateIsUnchanged()
    {
        var game = new Game();

        Assert.False(game.TryMakeMove(Move.ParseUci("e2e5")));
        Assert.Throws<InvalidOperationException>(() => game.MakeMove(Move.ParseUci("e1e2")));
        Assert.Empty(game.Moves);
        Assert.Equal(Position.StartFen, game.Position.ToFen());
    }

    [Fact]
    public void MovingOutOfTurnIsRejected()
    {
        var game = new Game();

        Assert.False(game.TryMakeMove(Move.ParseUci("e7e5")));
    }

    [Fact]
    public void FoolsMateIsCheckmateForBlack()
    {
        var game = Play("f2f3", "e7e5", "g2g4", "d8h4");

        Assert.Equal(GameStatus.Checkmate, game.Status);
        Assert.Equal(PieceColor.Black, game.Winner);
        Assert.Empty(game.LegalMoves);
        Assert.False(game.TryMakeMove(Move.ParseUci("a2a3")));
    }

    [Fact]
    public void StalemateIsADraw()
    {
        var game = PlayFrom("k7/8/3Q4/8/8/8/8/7K w - - 0 1", "d6b6");

        Assert.Equal(GameStatus.Stalemate, game.Status);
        Assert.Null(game.Winner);
    }

    [Fact]
    public void ThreefoldRepetitionIsADraw()
    {
        var game = Play("g1f3", "g8f6", "f3g1", "f6g8", "g1f3", "g8f6", "f3g1");
        Assert.Equal(GameStatus.Ongoing, game.Status);

        game.MakeMove(Move.ParseUci("f6g8"));

        Assert.Equal(GameStatus.ThreefoldRepetition, game.Status);
    }

    [Fact]
    public void FiftyMoveRuleIsADraw()
    {
        var game = PlayFrom("4k3/8/8/8/8/8/8/R3K3 w - - 99 80", "a1a2");

        Assert.Equal(GameStatus.FiftyMoveRule, game.Status);
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/2N1K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/2B1K3 w - - 0 1")]
    [InlineData("2b1k3/8/8/8/8/8/8/3BK3 w - - 0 1")]
    public void InsufficientMaterialIsADraw(string fen)
    {
        Assert.Equal(GameStatus.InsufficientMaterial, new Game(fen).Status);
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/P7/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/1NB1K3 w - - 0 1")]
    [InlineData("3bk3/8/8/8/8/8/8/3BK3 w - - 0 1")]
    public void SufficientMaterialKeepsTheGameGoing(string fen)
    {
        Assert.Equal(GameStatus.Ongoing, new Game(fen).Status);
    }

    [Fact]
    public void UndoRestoresThePreviousPositionAndStatus()
    {
        var game = Play("f2f3", "e7e5", "g2g4", "d8h4");

        game.Undo();

        Assert.Equal(GameStatus.Ongoing, game.Status);
        Assert.Equal(3, game.Moves.Count);
        Assert.Equal(PieceColor.Black, game.Position.SideToMove);
    }

    [Fact]
    public void UndoOnFreshGameThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new Game().Undo());
    }

    [Fact]
    public void SanHistoryIsRecorded()
    {
        var game = Play("e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "e1g1");

        Assert.Equal(["e4", "e5", "Nf3", "Nc6", "Bb5", "a6", "O-O"], game.SanMoves);
    }

    [Fact]
    public void TimeForfeitLosesUnlessOpponentHasOnlyAKing()
    {
        var game = new Game();
        game.ForfeitOnTime(PieceColor.White);
        Assert.Equal(GameStatus.TimeForfeit, game.Status);
        Assert.Equal(PieceColor.Black, game.Winner);

        var bareKing = new Game("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
        bareKing.ForfeitOnTime(PieceColor.White);
        Assert.Equal(GameStatus.TimeForfeit, bareKing.Status);
        Assert.Null(bareKing.Winner);
    }

    [Fact]
    public void ResignationLetsTheOpponentWin()
    {
        var game = new Game();

        game.Resign(PieceColor.Black);

        Assert.Equal(GameStatus.Resignation, game.Status);
        Assert.Equal(PieceColor.White, game.Winner);
    }
}
