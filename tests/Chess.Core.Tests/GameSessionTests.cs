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
        // Wait for the background search itself instead of a fixed sleep, which is flaky on a busy machine.
        using var searched = new ManualResetEventSlim();
        Move Choose(Position position, CancellationToken token)
        {
            var move = FirstLegalMove(position, token);
            searched.Set();
            return move;
        }
        var session = new GameSession(HumanVsComputer, null, null, Choose, TimeSpan.FromSeconds(1));
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));
        Assert.True(searched.Wait(TimeSpan.FromSeconds(5)));

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

public class DuelGameSessionTests
{
    private static Move FirstLegalMove(Position position, CancellationToken _) => position.GenerateLegalMoves()[0];

    private static GameSession Session(GameSetup setup, Game? game = null) =>
        new(setup, game ?? new Game(Position.StartFen, setup.Variant), null, FirstLegalMove, TimeSpan.Zero);

    private static readonly GameSetup HumanVsHuman = new(PlayerKind.Human, PlayerKind.Human, Variant: GameVariant.Duel);

    [Fact]
    public void CaptureRaisesDuelStartedAndWaitsForTheResult()
    {
        var session = Session(HumanVsHuman);
        PendingDuel? started = null;
        var played = new List<PlayedMove>();
        session.DuelStarted += d => started = d;
        session.MovePlayed += played.Add;

        foreach (var uci in new[] { "e2e4", "d7d5", "e4d5" }) session.TryPlayHumanMove(Move.ParseUci(uci));

        Assert.NotNull(started);
        Assert.Equal(2, played.Count);
        Assert.False(session.IsHumanTurn);

        session.ResolveDuel(DuelOutcome.DefenderWon);

        Assert.Equal(3, played.Count);
        Assert.Equal(DuelOutcome.DefenderWon, played[2].Outcome);
        Assert.True(session.IsHumanTurn);
    }

    [Fact]
    public void ClockIsPausedDuringADuel()
    {
        var setup = HumanVsHuman with { TimeControl = new TimeControl(TimeSpan.FromSeconds(30), TimeSpan.Zero) };
        var session = Session(setup);
        foreach (var uci in new[] { "e2e4", "d7d5", "e4d5" }) session.TryPlayHumanMove(Move.ParseUci(uci));
        var before = session.Clock!.Remaining(PieceColor.White);

        session.Update(TimeSpan.FromSeconds(20));

        Assert.Equal(before, session.Clock.Remaining(PieceColor.White));
        session.ResolveDuel(DuelOutcome.AttackerWon);
        session.Update(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(25), session.Clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void ComputerCapturesAnExposedKingImmediately()
    {
        var setup = new GameSetup(PlayerKind.Human, PlayerKind.Computer, Variant: GameVariant.Duel);
        var game = new Game("4r2k/8/8/8/8/8/4R3/4K3 w - - 0 1", GameVariant.Duel);
        PendingDuel? started = null;
        // A chooser that would never find the king capture on its own.
        var session = new GameSession(setup, game, null, (p, _) => p.GenerateLegalMoves()[0], TimeSpan.Zero);
        session.DuelStarted += d => started = d;

        session.TryPlayHumanMove(Move.ParseUci("e2e8"));
        session.ResolveDuel(DuelOutcome.DefenderWon);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (started?.Defender.Type != PieceType.King && DateTime.UtcNow < deadline)
        {
            session.Update(TimeSpan.FromMilliseconds(10));
            Thread.Sleep(5);
        }

        Assert.Equal(new Piece(PieceType.King, PieceColor.White), started?.Defender);
    }

    [Fact]
    public void UndoIsDisabledInTheDuelVariant()
    {
        var session = Session(HumanVsHuman);
        session.TryPlayHumanMove(Move.ParseUci("e2e4"));

        Assert.False(session.CanUndo);
    }

    [Fact]
    public void ResolvingWithoutADuelThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Session(HumanVsHuman).ResolveDuel(DuelOutcome.AttackerWon));
    }
}

public class ExplicitResignTests
{
    [Fact]
    public void AnExplicitSideCanResignAtAnyTime()
    {
        var session = new GameSession(new GameSetup(PlayerKind.Human, PlayerKind.Human), null, null,
            (p, _) => p.GenerateLegalMoves()[0], TimeSpan.Zero);
        var ended = false;
        session.GameEnded += () => ended = true;

        session.Resign(PieceColor.Black);

        Assert.True(ended);
        Assert.Equal(PieceColor.White, session.Game.Winner);
    }
}
