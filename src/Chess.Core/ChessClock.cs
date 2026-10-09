namespace Chess.Core;

/// <summary>
/// A two-sided chess clock with Fischer increment. It has no timer of its own:
/// the UI calls <see cref="Tick"/> with the elapsed frame time, which keeps it deterministic and testable.
/// </summary>
public sealed class ChessClock
{
    private TimeSpan _white;
    private TimeSpan _black;

    public ChessClock(TimeSpan initial, TimeSpan increment)
    {
        if (initial <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initial), "Initial time must be positive.");
        if (increment < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(increment), "Increment cannot be negative.");
        Initial = initial;
        Increment = increment;
        _white = initial;
        _black = initial;
    }

    public static ChessClock Restore(TimeSpan initial, TimeSpan increment, TimeSpan white, TimeSpan black)
    {
        if (white < TimeSpan.Zero || black < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(white), "Remaining time cannot be negative.");
        return new ChessClock(initial, increment) { _white = white, _black = black };
    }

    public TimeSpan Initial { get; }
    public TimeSpan Increment { get; }
    public PieceColor Active { get; private set; }
    public bool IsRunning { get; private set; }

    public PieceColor? Flagged =>
        _white <= TimeSpan.Zero ? PieceColor.White :
        _black <= TimeSpan.Zero ? PieceColor.Black : null;

    public TimeSpan Remaining(PieceColor color) => color == PieceColor.White ? _white : _black;

    public void Start(PieceColor toMove)
    {
        Active = toMove;
        IsRunning = Flagged is null;
    }

    public void Stop() => IsRunning = false;

    public void Tick(TimeSpan elapsed)
    {
        if (!IsRunning) return;
        var remaining = Remaining(Active) - elapsed;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        Set(Active, remaining);
        if (remaining == TimeSpan.Zero) IsRunning = false;
    }

    /// <summary>The active side has moved: credit its increment and hand the clock to the opponent.</summary>
    public void SwitchTurn()
    {
        if (Flagged is not null) return;
        Set(Active, Remaining(Active) + Increment);
        Active = Active.Opponent();
    }

    private void Set(PieceColor color, TimeSpan value)
    {
        if (color == PieceColor.White) _white = value;
        else _black = value;
    }
}
