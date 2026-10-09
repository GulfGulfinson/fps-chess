using Chess.Core;
using Chess.Core.Session;

namespace Chess.Core.Tests;

public class GameSessionTests
{
    private static readonly GameSetup HumanVsHuman = new(PlayerKind.Human, PlayerKind.Human);
    private static readonly GameSetup HumanVsComputer = new(PlayerKind.Human, PlayerKind.Computer);

    /// <summary>A computer that always plays the first legal move, so tests are deterministic.</summary>
    private static Move FirstLegalMove(Position position, CancellationToken _) => position.GenerateLegalMoves()[0];

    private static GameSession Session(GameSetup setup, Game? game = null, ChessClock? clock = null) =>
        new(setup, game, clock, FirstLegalMove, TimeSpan.Zero);

    /// <summary>Pumps Update until the computer has replied (it searches on a background thread).</summary>
    private static void WaitForComputer(GameSession session)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!session.IsHumanTurn && !session.Game.IsOver)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The computer did not move.");
            session.Update(TimeSpan.FromMilliseconds(10));
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void HumansAlternateAndEachMoveIsReported()
    {
        var session = Session(HumanVsHuman);
        var played = new List<PlayedMove>();
        session.MovePlayed += played.Add;

        Assert.True(session.TryPlayHumanMove(Move.ParseUci("e2e4")));
        Assert.True(session.TryPlayHumanMove(Move.ParseUci("e7e5")));

        Assert.Equal(["e4", "e5"], played.Select(p => p.San));
        Assert.Equal(PieceColor.White, session.Game.Position.SideToMove);
    }

    [Fact]
    public void HumanCannotMoveDuringTheComputersTurn()
    {
        var session = Session(new GameSetup(PlayerKind.Computer, PlayerKind.Human));

        Assert.False(session.IsHumanTurn);
        Assert.False(session.TryPlayHumanMove(Move.ParseUci("e2e4")));
    }

    [Fact]
    public void ComputerRepliesAfterUpdate()
    {
        var session = Session(HumanVsComputer);

        session.TryPlayHumanMove(Move.ParseUci("e2e4"));
        Assert.True(session.IsComputerThinking);
        WaitForComputer(session);

        Assert.Equal(2, session.Game.Moves.Count);
        Assert.True(session.IsHumanTurn);
    }

    [Fact]
    public void ComputerWaitsForTheMinimumThinkTime()
    {
        var session = new GameSession(HumanVsComputer, null, null, FirstLegalMove, TimeSpan.FromSeconds(1));
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));
        Thread.Sleep(100);

        session.Update(TimeSpan.FromMilliseconds(500));
        Assert.Single(session.Game.Moves);

        session.Update(TimeSpan.FromMilliseconds(600));
        Assert.Equal(2, session.Game.Moves.Count);
    }

    [Fact]
    public void CaptureDetailsAreReported()
    {
        var session = Session(HumanVsHuman, new Game("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 2"));
        PlayedMove? played = null;
        session.MovePlayed += p => played = p;

        session.TryPlayHumanMove(Move.ParseUci("e5d6"));

        Assert.NotNull(played);
        Assert.Equal(new Piece(PieceType.Pawn, PieceColor.Black), played.Captured);
        Assert.Equal(Square.Parse("d5"), played.CapturedOn);
    }

    [Fact]
    public void CastlingReportsTheRookMove()
    {
        var session = Session(HumanVsHuman, new Game("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"));
        PlayedMove? played = null;
        session.MovePlayed += p => played = p;

        session.TryPlayHumanMove(Move.ParseUci("e1c1"));

        Assert.Equal((Square.Parse("a1"), Square.Parse("d1")), played?.RookMove);
    }

    [Fact]
    public void ClockRunsOutAndEndsTheGame()
    {
        var setup = HumanVsHuman with { TimeControl = new TimeControl(TimeSpan.FromSeconds(10), TimeSpan.Zero) };
        var session = Session(setup);
        var ended = false;
        session.GameEnded += () => ended = true;

        session.Update(TimeSpan.FromSeconds(11));

        Assert.True(ended);
        Assert.Equal(GameStatus.TimeForfeit, session.Game.Status);
        Assert.Equal(PieceColor.Black, session.Game.Winner);
    }

    [Fact]
    public void PausedClockDoesNotRun()
    {
        var setup = HumanVsHuman with { TimeControl = new TimeControl(TimeSpan.FromSeconds(10), TimeSpan.Zero) };
        var session = Session(setup);

        session.Paused = true;
        session.Update(TimeSpan.FromSeconds(11));

        Assert.Equal(GameStatus.Ongoing, session.Game.Status);
    }

    [Fact]
    public void CheckmateRaisesGameEnded()
    {
        var session = Session(HumanVsHuman);
        var ended = 0;
        session.GameEnded += () => ended++;

        foreach (var uci in new[] { "f2f3", "e7e5", "g2g4", "d8h4" }) session.TryPlayHumanMove(Move.ParseUci(uci));

        Assert.Equal(1, ended);
        Assert.Equal(GameStatus.Checkmate, session.Game.Status);
    }

    [Fact]
    public void UndoAgainstComputerTakesBackBothMoves()
    {
        var session = Session(HumanVsComputer);
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));
        WaitForComputer(session);

        Assert.True(session.CanUndo);
        session.Undo();

        Assert.Empty(session.Game.Moves);
        Assert.True(session.IsHumanTurn);
    }

    [Fact]
    public void UndoIsNotAllowedInTimedGames()
    {
        var setup = HumanVsHuman with { TimeControl = new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.Zero) };
        var session = Session(setup);
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));

        Assert.False(session.CanUndo);
    }

    [Fact]
    public void HumanCanResign()
    {
        var session = Session(HumanVsComputer);

        session.Resign();

        Assert.Equal(GameStatus.Resignation, session.Game.Status);
        Assert.Equal(PieceColor.Black, session.Game.Winner);
    }

    [Fact]
    public void SnapshotContainsGameSetupAndClock()
    {
        var setup = HumanVsHuman with { TimeControl = new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.Zero) };
        var session = Session(setup);
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));

        var save = session.ToSave();

        Assert.Same(session.Game, save.Game);
        Assert.Equal(setup, save.Setup);
        Assert.NotNull(save.Clock);
    }
}
