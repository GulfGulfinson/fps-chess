namespace Chess.Core;

/// <summary>
/// A game in progress: the move history, the current position and the result.
/// Only legal moves are accepted. Draws by repetition and the fifty-move rule are applied automatically.
/// In the <see cref="GameVariant.Duel"/> variant a capture waits in <see cref="PendingDuel"/> until
/// <see cref="ResolveDuel"/> reports who won the fight.
/// </summary>
public sealed class Game
{
    private readonly List<Position> _positions = [];
    private readonly List<Move> _moves = [];
    private readonly List<string> _sanMoves = [];
    private readonly List<DuelOutcome?> _duelOutcomes = [];
    private List<Move> _legalMoves = [];

    public Game(string startFen = Position.StartFen, GameVariant variant = GameVariant.Classic)
    {
        StartFen = startFen;
        Variant = variant;
        _positions.Add(Position.FromFen(startFen, allowExposedKing: variant == GameVariant.Duel));
        Refresh();
    }

    public string StartFen { get; }
    public GameVariant Variant { get; }

    /// <summary>The capture currently being fought out, if any. No move is accepted until it is resolved.</summary>
    public PendingDuel? PendingDuel { get; private set; }

    /// <summary>Per move: the duel result for captures in the duel variant, otherwise null.</summary>
    public IReadOnlyList<DuelOutcome?> DuelOutcomes => _duelOutcomes;
    public Position Position => _positions[^1];
    public IReadOnlyList<Move> Moves => _moves;
    public IReadOnlyList<string> SanMoves => _sanMoves;
    public IReadOnlyList<Move> LegalMoves => _legalMoves;
    public Move? LastMove => _moves.Count > 0 ? _moves[^1] : null;

    public GameStatus Status { get; private set; }

    /// <summary>The winning side, or null for a draw or an ongoing game.</summary>
    public PieceColor? Winner { get; private set; }

    public bool IsOver => Status != GameStatus.Ongoing;

    /// <summary>The side that resigned or ran out of time; null for every other result.</summary>
    public PieceColor? ForfeitedBy { get; private set; }

    public IEnumerable<Move> LegalMovesFrom(Square from) => _legalMoves.Where(m => m.From == from);

    public bool IsLegal(Move move) => !IsOver && PendingDuel is null && _legalMoves.Contains(move);

    public bool TryMakeMove(Move move)
    {
        if (!IsLegal(move)) return false;

        if (Variant == GameVariant.Duel && Position.IsCapture(move))
        {
            PendingDuel = PendingDuel.Describe(Position, move);
            _legalMoves = [];
            return true;
        }

        Complete(move, null);
        return true;
    }

    public void ResolveDuel(DuelOutcome outcome)
    {
        if (PendingDuel is not { } duel) throw new InvalidOperationException("There is no duel to resolve.");
        PendingDuel = null;

        if (outcome == DuelOutcome.AttackerWon)
        {
            Complete(duel.Move, outcome);
            return;
        }

        // Check marks describe the position after a successful capture, so they do not apply here.
        _sanMoves.Add(Notation.ToSan(Position, duel.Move).TrimEnd('+', '#') + " \u2717");
        _moves.Add(duel.Move);
        _duelOutcomes.Add(outcome);
        _positions.Add(Position.ApplyFailedCapture(duel.Move));
        Refresh();
    }

    private void Complete(Move move, DuelOutcome? outcome)
    {
        var capturesKing = Position.PieceAt(move.To)?.Type == PieceType.King;
        var mover = Position.SideToMove;

        _sanMoves.Add(Notation.ToSan(Position, move));
        _moves.Add(move);
        _duelOutcomes.Add(outcome);
        _positions.Add(Position.Apply(move));

        if (capturesKing)
        {
            Status = GameStatus.KingCaptured;
            Winner = mover;
            ForfeitedBy = null;
            _legalMoves = [];
            return;
        }
        Refresh();
    }

    public void MakeMove(Move move)
    {
        if (!TryMakeMove(move))
            throw new InvalidOperationException($"Illegal move {move} in position {Position.ToFen()}.");
    }

    public bool CanUndo => _moves.Count > 0 && PendingDuel is null;

    /// <summary>Takes back the last move. Also clears a timeout or resignation result.</summary>
    public void Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("There is no move to undo.");
        _moves.RemoveAt(_moves.Count - 1);
        _sanMoves.RemoveAt(_sanMoves.Count - 1);
        _duelOutcomes.RemoveAt(_duelOutcomes.Count - 1);
        _positions.RemoveAt(_positions.Count - 1);
        Refresh();
    }

    public void ForfeitOnTime(PieceColor loser)
    {
        if (IsOver) return;
        PendingDuel = null;
        Status = GameStatus.TimeForfeit;
        ForfeitedBy = loser;
        // A side with a bare king can never mate, so the flag fall is a draw (FIDE 6.9).
        var opponentHasOnlyKing = Position.Pieces().All(p => p.Piece.Color == loser || p.Piece.Type == PieceType.King);
        Winner = opponentHasOnlyKing ? null : loser.Opponent();
        _legalMoves = [];
    }

    public void Resign(PieceColor loser)
    {
        if (IsOver) return;
        PendingDuel = null;
        Status = GameStatus.Resignation;
        ForfeitedBy = loser;
        Winner = loser.Opponent();
        _legalMoves = [];
    }

    private void Refresh()
    {
        var position = Position;
        _legalMoves = position.GenerateLegalMoves();
        if (Variant == GameVariant.Duel) _legalMoves = _legalMoves.Union(position.GenerateKingCaptures()).ToList();
        Winner = null;
        ForfeitedBy = null;

        if (_legalMoves.Count == 0)
        {
            if (position.IsInCheck(position.SideToMove))
            {
                Status = GameStatus.Checkmate;
                Winner = position.SideToMove.Opponent();
            }
            else
            {
                Status = GameStatus.Stalemate;
            }
        }
        else if (HasInsufficientMaterial(position)) Status = GameStatus.InsufficientMaterial;
        else if (position.HalfmoveClock >= 100) Status = GameStatus.FiftyMoveRule;
        else if (IsThreefoldRepetition()) Status = GameStatus.ThreefoldRepetition;
        else Status = GameStatus.Ongoing;

        if (IsOver) _legalMoves = [];
    }

    private bool IsThreefoldRepetition()
    {
        // Only positions since the last pawn move or capture can repeat.
        var key = Position.RepetitionKey();
        var reachable = Math.Min(Position.HalfmoveClock, _positions.Count - 1);
        var count = 0;
        for (var i = _positions.Count - 1; i >= _positions.Count - 1 - reachable; i--)
        {
            if (_positions[i].RepetitionKey() == key) count++;
        }
        return count >= 3;
    }

    /// <summary>True when neither side can possibly deliver mate.</summary>
    public static bool HasInsufficientMaterial(Position position)
    {
        var others = position.Pieces().Where(p => p.Piece.Type != PieceType.King).ToList();
        if (others.Count == 0) return true;
        if (others.Count == 1 && others[0].Piece.Type is PieceType.Bishop or PieceType.Knight) return true;
        // Any number of bishops that all stand on the same square colour cannot mate.
        return others.All(p => p.Piece.Type == PieceType.Bishop) &&
               others.Select(p => p.Square.IsLight).Distinct().Count() == 1;
    }
}
