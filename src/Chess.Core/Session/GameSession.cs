using Chess.Core.Ai;
using Chess.Core.Persistence;

namespace Chess.Core.Session;

/// <summary>
/// Runs a game for a UI: whose turn it is, the computer opponent on a background thread,
/// the chess clock, undo and save snapshots. The UI calls <see cref="Update"/> every frame;
/// all events are raised from inside Update or the move methods, so on the UI thread.
/// </summary>
public sealed class GameSession : IDisposable
{
    private static readonly TimeSpan DefaultMinimumThinkTime = TimeSpan.FromMilliseconds(600);

    private readonly Func<Position, CancellationToken, Move> _chooseComputerMove;
    private readonly TimeSpan _minimumThinkTime;

    private Task<Move>? _computerTask;
    private CancellationTokenSource? _computerCancellation;
    private TimeSpan _thinkingFor;

    public GameSession(GameSetup setup, Game? game = null, ChessClock? clock = null)
        : this(setup, game, clock, new ChessAi(setup.AiLevel).ChooseMove, DefaultMinimumThinkTime)
    {
    }

    /// <param name="chooseComputerMove">Move picker for computer sides; tests inject a deterministic one.</param>
    /// <param name="minimumThinkTime">The computer never answers faster than this, so its moves are visible.</param>
    public GameSession(GameSetup setup, Game? game, ChessClock? clock,
        Func<Position, CancellationToken, Move> chooseComputerMove, TimeSpan minimumThinkTime)
    {
        Setup = setup;
        Game = game ?? new Game();
        Clock = clock ?? (setup.TimeControl is { } tc ? new ChessClock(tc.Initial, tc.Increment) : null);
        _chooseComputerMove = chooseComputerMove;
        _minimumThinkTime = minimumThinkTime;

        if (!Game.IsOver) Clock?.Start(Turn);
        StartComputerIfNeeded();
    }

    public GameSetup Setup { get; }
    public Game Game { get; }
    public ChessClock? Clock { get; }

    /// <summary>While paused, neither the clock nor the computer's move advance.</summary>
    public bool Paused { get; set; }

    public PieceColor Turn => Game.Position.SideToMove;
    public bool IsHumanTurn => !Game.IsOver && Setup.PlayerFor(Turn) == PlayerKind.Human;
    public bool IsComputerThinking => _computerTask is not null;

    public event Action<PlayedMove>? MovePlayed;
    public event Action? GameEnded;

    /// <summary>Raised after undo: views should redraw the whole board from <see cref="Game"/>.</summary>
    public event Action? PositionReset;

    public bool TryPlayHumanMove(Move move)
    {
        if (!IsHumanTurn || !Game.IsLegal(move)) return false;
        Play(move);
        return true;
    }

    public void Update(TimeSpan elapsed)
    {
        if (Game.IsOver || Paused) return;

        if (Clock is not null)
        {
            Clock.Tick(elapsed);
            if (Clock.Flagged is { } flagged)
            {
                CancelComputer();
                Game.ForfeitOnTime(flagged);
                GameEnded?.Invoke();
                return;
            }
        }

        if (_computerTask is null) return;
        _thinkingFor += elapsed;
        if (!_computerTask.IsCompleted || _thinkingFor < _minimumThinkTime) return;

        // GetResult rethrows a crash in the search on the UI thread instead of hiding it.
        var move = _computerTask.GetAwaiter().GetResult();
        _computerTask = null;
        _computerCancellation?.Dispose();
        _computerCancellation = null;
        Play(move);
    }

    /// <summary>Undo is offered for untimed games with a human player, back to that human's last turn.</summary>
    public bool CanUndo => Clock is null && !IsComputerThinking && HumanMoveIndexes().Any();

    public void Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("Undo is not possible right now.");

        var target = HumanMoveIndexes().Last();
        while (Game.Moves.Count > target) Game.Undo();

        PositionReset?.Invoke();
        StartComputerIfNeeded();
    }

    /// <summary>The human whose turn it is resigns; against the computer, the human side resigns at any time.</summary>
    public void Resign()
    {
        if (Game.IsOver) return;
        var loser = Setup.PlayerFor(Turn) == PlayerKind.Human || Setup.PlayerFor(Turn.Opponent()) == PlayerKind.Computer
            ? Turn
            : Turn.Opponent();
        CancelComputer();
        Game.Resign(loser);
        Clock?.Stop();
        GameEnded?.Invoke();
    }

    public SavedGame ToSave() => new(Setup, Game, Clock);

    public void Dispose() => CancelComputer();

    private void Play(Move move)
    {
        var before = Game.Position;
        Game.MakeMove(move);
        var played = PlayedMove.Describe(before, move, Game.SanMoves[^1]);

        if (Game.IsOver) Clock?.Stop();
        else Clock?.SwitchTurn();

        MovePlayed?.Invoke(played);
        if (Game.IsOver) GameEnded?.Invoke();
        else StartComputerIfNeeded();
    }

    private void StartComputerIfNeeded()
    {
        if (Game.IsOver || IsHumanTurn || _computerTask is not null) return;

        var position = Game.Position;
        var cancellation = new CancellationTokenSource();
        _computerCancellation = cancellation;
        _thinkingFor = TimeSpan.Zero;
        _computerTask = Task.Run(() => _chooseComputerMove(position, cancellation.Token), cancellation.Token);
    }

    private void CancelComputer()
    {
        _computerCancellation?.Cancel();
        _computerCancellation?.Dispose();
        _computerCancellation = null;
        _computerTask = null;
    }

    /// <summary>Indexes into the move list where a human was to move (undoing to there hands them the turn).</summary>
    private IEnumerable<int> HumanMoveIndexes()
    {
        var first = Position.FromFen(Game.StartFen).SideToMove;
        for (var i = 0; i < Game.Moves.Count; i++)
        {
            var mover = i % 2 == 0 ? first : first.Opponent();
            if (Setup.PlayerFor(mover) == PlayerKind.Human) yield return i;
        }
    }
}
