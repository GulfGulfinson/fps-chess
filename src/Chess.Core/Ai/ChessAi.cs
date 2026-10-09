namespace Chess.Core.Ai;

/// <summary>
/// Computer opponent: negamax search with alpha-beta pruning to a fixed depth (in plies),
/// followed by a capture-only quiescence search so it does not stop in the middle of an exchange.
/// </summary>
public sealed class ChessAi
{
    private const int MateScore = 1_000_000;
    private const int Infinity = int.MaxValue / 2;
    private const int MaxQuiescenceDepth = 8;

    private readonly Random _random;

    public ChessAi(int depth, Random? random = null)
    {
        if (depth is < GameSetup.MinAiLevel or > 8) throw new ArgumentOutOfRangeException(nameof(depth));
        Depth = depth;
        _random = random ?? Random.Shared;
    }

    public int Depth { get; }

    /// <summary>
    /// Picks the best move; equally good moves are chosen at random so games vary.
    /// Runs synchronously, so UIs should call it off the main thread.
    /// </summary>
    public Move ChooseMove(Position position, CancellationToken cancellationToken = default)
    {
        var moves = OrderMoves(position, position.GenerateLegalMoves());
        if (moves.Count == 0) throw new InvalidOperationException("There is no legal move to choose.");

        var best = -Infinity;
        var bestMoves = new List<Move>();
        foreach (var move in moves)
        {
            // Window starts one below the best score so ties come back exact and can be collected.
            var score = -Negamax(position.Apply(move), Depth - 1, -Infinity, -(best - 1), 1, cancellationToken);
            if (score > best)
            {
                best = score;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (score == best)
            {
                bestMoves.Add(move);
            }
        }
        return bestMoves[_random.Next(bestMoves.Count)];
    }

    private static int Negamax(Position position, int depth, int alpha, int beta, int ply, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var moves = position.GenerateLegalMoves();
        if (moves.Count == 0)
            // Prefer faster mates and slower losses by scoring with the distance from the root.
            return position.IsInCheck(position.SideToMove) ? -(MateScore - ply) : 0;
        if (position.HalfmoveClock >= 100 || Game.HasInsufficientMaterial(position)) return 0;
        if (depth <= 0) return Quiescence(position, alpha, beta, 0, ct);

        foreach (var move in OrderMoves(position, moves))
        {
            var score = -Negamax(position.Apply(move), depth - 1, -beta, -alpha, ply + 1, ct);
            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    private static int Quiescence(Position position, int alpha, int beta, int qDepth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var standPat = Evaluator.Evaluate(position);
        if (standPat >= beta) return beta;
        if (standPat > alpha) alpha = standPat;
        if (qDepth >= MaxQuiescenceDepth) return alpha;

        var tactical = position.GenerateLegalMoves()
            .Where(m => position.IsCapture(m) || m.Promotion is not null)
            .ToList();
        foreach (var move in OrderMoves(position, tactical))
        {
            var score = -Quiescence(position.Apply(move), -beta, -alpha, qDepth + 1, ct);
            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    /// <summary>Promotions and captures first (most valuable victim, least valuable attacker) for better pruning.</summary>
    private static List<Move> OrderMoves(Position position, List<Move> moves)
    {
        int Priority(Move move)
        {
            var priority = move.Promotion is { } p ? Evaluator.PieceValue(p) * 10 : 0;
            if (position.IsCapture(move))
            {
                var victim = position.PieceAt(move.To)?.Type ?? PieceType.Pawn;
                var attacker = position.PieceAt(move.From)?.Type ?? PieceType.Pawn;
                priority += Evaluator.PieceValue(victim) * 10 - Evaluator.PieceValue(attacker);
            }
            return priority;
        }

        return moves.OrderByDescending(Priority).ToList();
    }
}
