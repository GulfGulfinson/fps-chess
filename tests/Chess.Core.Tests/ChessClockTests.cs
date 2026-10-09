using Chess.Core;

namespace Chess.Core.Tests;

public class ChessClockTests
{
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ThreeSeconds = TimeSpan.FromSeconds(3);

    [Fact]
    public void OnlyTheActiveSideLosesTime()
    {
        var clock = new ChessClock(FiveMinutes, TimeSpan.Zero);
        clock.Start(PieceColor.White);

        clock.Tick(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(290), clock.Remaining(PieceColor.White));
        Assert.Equal(FiveMinutes, clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void StoppedClockDoesNotRun()
    {
        var clock = new ChessClock(FiveMinutes, TimeSpan.Zero);

        clock.Tick(TimeSpan.FromSeconds(10));

        Assert.Equal(FiveMinutes, clock.Remaining(PieceColor.White));
    }

    [Fact]
    public void SwitchingTurnAddsIncrementToTheMoverAndStartsTheOpponent()
    {
        var clock = new ChessClock(FiveMinutes, ThreeSeconds);
        clock.Start(PieceColor.White);
        clock.Tick(TimeSpan.FromSeconds(10));

        clock.SwitchTurn();
        clock.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(293), clock.Remaining(PieceColor.White));
        Assert.Equal(TimeSpan.FromSeconds(299), clock.Remaining(PieceColor.Black));
        Assert.Equal(PieceColor.Black, clock.Active);
    }

    [Fact]
    public void RunningOutOfTimeFlagsTheSideAndClampsAtZero()
    {
        var clock = new ChessClock(TimeSpan.FromSeconds(1), TimeSpan.Zero);
        clock.Start(PieceColor.White);

        clock.Tick(TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.Zero, clock.Remaining(PieceColor.White));
        Assert.Equal(PieceColor.White, clock.Flagged);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void RestoreKeepsRemainingTimes()
    {
        var clock = ChessClock.Restore(FiveMinutes, ThreeSeconds, TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(7));

        Assert.Equal(TimeSpan.FromSeconds(42), clock.Remaining(PieceColor.White));
        Assert.Equal(TimeSpan.FromSeconds(7), clock.Remaining(PieceColor.Black));
        Assert.Null(clock.Flagged);
    }

    [Fact]
    public void NonPositiveInitialTimeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChessClock(TimeSpan.Zero, TimeSpan.Zero));
    }
}
