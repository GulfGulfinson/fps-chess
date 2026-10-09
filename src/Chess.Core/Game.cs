namespace Chess.Core;

/// <summary>
/// A game in progress: the move history, the current position and the result.
/// Only legal moves are accepted. Draws by repetition and the fifty-move rule are applied automatically.
/// </summary>
public sealed class Game
{
    private readonly List<Position> _positions = [];
    private readonly List<Move> _moves = [];
    private readonly List<string> _sanMoves = [];
    private List<Move> _legalMoves = [];

    public Game(string startFen = Position.StartFen)
    {
        StartFen = startFen;
        _positions.Add(Position.FromFen(startFen));
        Refresh();
    }

    public string StartFen { get; }
    public Position Position => _positions[^1];
    public IReadOnlyList<Move> Moves => _moves;
    public IReadOnlyList<string> SanMoves => _sanMoves;
    public IReadOnlyList<Move> LegalMoves => _legalMoves;
    public Move? LastMove => _moves.Count > 0 ? _moves[^1] : null;

    public GameStatus Status { get; private set; }

    /// <summary>The winning side, or null for a draw or an ongoing game.</summary>
    public PieceColor? Winner { get; private set; }

    public bool IsOver => Status != GameStatus.Ongoing;

    public IEnumerable<Move> LegalMovesFrom(Square from) => _legalMoves.Where(m => m.From == from);

    public bool IsLegal(Move move) => !IsOver && _legalMoves.Contains(move);

    public bool TryMakeMove(Move move)
    {
        if (!IsLegal(move)) return false;

        _sanMoves.Add(Notation.ToSan(Position, move));
        _moves.Add(move);
        _positions.Add(Position.Apply(move));
        Refresh();
        return true;
    }

    public void MakeMove(Move move)
    {
        if (!TryMakeMove(move))
            throw new InvalidOperationException($"Illegal move {move} in position {Position.ToFen()}.");
    }

    public bool CanUndo => _moves.Count > 0;

    /// <summary>Takes back the last move. Also clears a timeout or resignation result.</summary>
    public void Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("There is no move to undo.");
        _moves.RemoveAt(_moves.Count - 1);
        _sanMoves.RemoveAt(_sanMoves.Count - 1);
        _positions.RemoveAt(_positions.Count - 1);
        Refresh();
    }

    public void ForfeitOnTime(PieceColor loser)
    {
        if (IsOver) return;
        Status = GameStatus.TimeForfeit;
        // A side with a bare king can never mate, so the flag fall is a draw (FIDE 6.9).
        var opponentHasOnlyKing = Position.Pieces().All(p => p.Piece.Color == loser || p.Piece.Type == PieceType.King);
        Winner = opponentHasOnlyKing ? null : loser.Opponent();
        _legalMoves = [];
    }

    public void Resign(PieceColor loser)
    {
        if (IsOver) return;
        Status = GameStatus.Resignation;
        Winner = loser.Opponent();
        _legalMoves = [];
    }

    private void Refresh()
    {
        var position = Position;
        _legalMoves = position.GenerateLegalMoves();
        Winner = null;

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
