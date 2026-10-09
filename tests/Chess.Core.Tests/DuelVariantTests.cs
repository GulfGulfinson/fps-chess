using Chess.Core;

namespace Chess.Core.Tests;

public class DuelVariantTests
{
    private static Game DuelGame(string fen = Position.StartFen) => new(fen, GameVariant.Duel);

    private static void Play(Game game, params string[] uciMoves)
    {
        foreach (var uci in uciMoves) game.MakeMove(Move.ParseUci(uci));
    }

    [Fact]
    public void QuietMovesCompleteImmediately()
    {
        var game = DuelGame();

        game.MakeMove(Move.ParseUci("e2e4"));

        Assert.Null(game.PendingDuel);
        Assert.Single(game.Moves);
    }

    [Fact]
    public void CaptureStartsADuelAndFreezesTheGame()
    {
        var game = DuelGame();
        Play(game, "e2e4", "d7d5", "e4d5");

        var duel = Assert.IsType<PendingDuel>(game.PendingDuel);
        Assert.Equal(new Piece(PieceType.Pawn, PieceColor.White), duel.Attacker);
        Assert.Equal(new Piece(PieceType.Pawn, PieceColor.Black), duel.Defender);
        Assert.Equal(Square.Parse("d5"), duel.DefenderSquare);
        Assert.Equal(2, game.Moves.Count);
        Assert.Empty(game.LegalMoves);
        Assert.False(game.TryMakeMove(Move.ParseUci("d8d5")));
    }

    [Fact]
    public void AttackerWinningCompletesTheCapture()
    {
        var game = DuelGame();
        Play(game, "e2e4", "d7d5", "e4d5");

        game.ResolveDuel(DuelOutcome.AttackerWon);

        Assert.Null(game.PendingDuel);
        Assert.Equal("rnbqkbnr/ppp1pppp/8/3P4/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 2", game.Position.ToFen());
        Assert.Equal(DuelOutcome.AttackerWon, game.DuelOutcomes[2]);
        Assert.Equal("exd5", game.SanMoves[2]);
    }

    [Fact]
    public void DefenderWinningRemovesTheAttacker()
    {
        var game = DuelGame();
        Play(game, "e2e4", "d7d5", "e4d5");

        game.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Equal("rnbqkbnr/ppp1pppp/8/3p4/8/8/PPPP1PPP/RNBQKBNR b KQkq - 0 2", game.Position.ToFen());
        Assert.Equal("exd5 ✗", game.SanMoves[2]);
        Assert.Equal(GameStatus.Ongoing, game.Status);
    }

    [Fact]
    public void FailedCaptureFromTheCornerLosesCastlingRights()
    {
        var game = DuelGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        Play(game, "a1a8");

        game.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Equal("r3k2r/8/8/8/8/8/8/4K2R b Kkq - 0 1", game.Position.ToFen());
    }

    [Fact]
    public void FailedEnPassantKeepsThePassedPawn()
    {
        var game = DuelGame("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2");
        Play(game, "e5d6");
        Assert.Equal(Square.Parse("d5"), game.PendingDuel?.DefenderSquare);

        game.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Equal("4k3/8/8/3p4/8/8/8/4K3 b - - 0 2", game.Position.ToFen());
    }

    [Fact]
    public void WinningCaptureCanDeliverCheckmate()
    {
        var game = DuelGame();
        Play(game, "e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7");

        game.ResolveDuel(DuelOutcome.AttackerWon);

        Assert.Equal(GameStatus.Checkmate, game.Status);
        Assert.Equal(PieceColor.White, game.Winner);
    }

    [Fact]
    public void LostDuelCanExposeTheKingWhichMayThenBeCaptured()
    {
        // The white rook is pinned on the e-file and captures the pinning rook, but loses.
        var game = DuelGame("4r2k/8/8/8/8/8/4R3/4K3 w - - 0 1");
        Play(game, "e2e8");
        game.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Contains(Move.ParseUci("e8e1"), game.LegalMoves);
        Play(game, "e8e1");
        Assert.Equal(new Piece(PieceType.King, PieceColor.White), game.PendingDuel?.Defender);

        game.ResolveDuel(DuelOutcome.AttackerWon);

        Assert.Equal(GameStatus.KingCaptured, game.Status);
        Assert.Equal(PieceColor.Black, game.Winner);
        Assert.True(game.IsOver);
    }

    [Fact]
    public void KingThatWinsItsDuelSurvives()
    {
        var game = DuelGame("4r2k/8/8/8/8/8/4R3/4K3 w - - 0 1");
        Play(game, "e2e8");
        game.ResolveDuel(DuelOutcome.DefenderWon);
        Play(game, "e8e1");

        game.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Equal(GameStatus.InsufficientMaterial, game.Status);
        Assert.Equal("7k/8/8/8/8/8/8/4K3 w - - 0 2", game.Position.ToFen());
    }

    [Fact]
    public void ExposedKingMayBeCapturedEvenWhileOwnKingIsInCheck()
    {
        // Black to move, black king in check from the white queen, white king exposed to the black rook.
        var game = DuelGame("4r2k/7Q/8/8/8/8/8/4K3 b - - 0 1");

        Assert.Contains(Move.ParseUci("e8e1"), game.LegalMoves);
        Assert.Contains(Move.ParseUci("h8h7"), game.LegalMoves);
    }

    [Fact]
    public void ClassicGamesNeverOfferKingCaptures()
    {
        Assert.Throws<FormatException>(() => new Game("4r2k/7Q/8/8/8/8/8/4K3 b - - 0 1"));
    }

    [Fact]
    public void ResolvingWithoutADuelThrows()
    {
        Assert.Throws<InvalidOperationException>(() => DuelGame().ResolveDuel(DuelOutcome.AttackerWon));
    }

    [Fact]
    public void PromotionCaptureAppliesThePromotionWhenWon()
    {
        var game = DuelGame("1r5k/P7/8/8/8/8/8/K7 w - - 0 1");
        Play(game, "a7b8q");

        game.ResolveDuel(DuelOutcome.AttackerWon);

        Assert.Equal(new Piece(PieceType.Queen, PieceColor.White), game.Position.PieceAt(Square.Parse("b8")));
    }

    [Fact]
    public void UndoTakesBackAResolvedDuel()
    {
        var game = DuelGame();
        Play(game, "e2e4", "d7d5", "e4d5");
        game.ResolveDuel(DuelOutcome.DefenderWon);

        game.Undo();

        Assert.Equal(2, game.Moves.Count);
        Assert.Equal(2, game.DuelOutcomes.Count);
        Assert.Equal(PieceColor.White, game.Position.SideToMove);
    }
}
