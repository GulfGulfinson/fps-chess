namespace Chess.Core;

public sealed partial class Position
{
    private static readonly (int File, int Rank)[] KnightJumps =
        [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];

    private static readonly (int File, int Rank)[] KingSteps =
        [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];

    private static readonly (int File, int Rank)[] RookDirections = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int File, int Rank)[] BishopDirections = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    private static readonly PieceType[] PromotionPieces =
        [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    /// <summary>All moves that do not leave the mover's own king in check.</summary>
    public List<Move> GenerateLegalMoves()
    {
        var legal = new List<Move>(48);
        foreach (var move in GeneratePseudoLegalMoves())
        {
            if (!Apply(move).IsInCheck(SideToMove)) legal.Add(move);
        }
        return legal;
    }

    /// <summary>
    /// Duel variant: moves that capture the opponent's king, which can stand exposed after a lost duel.
    /// They are allowed even when the mover's own king is in check.
    /// </summary>
    public List<Move> GenerateKingCaptures()
    {
        if (TryKingSquare(SideToMove.Opponent()) is not { } king) return [];
        return GeneratePseudoLegalMoves().Where(m => m.To == king).ToList();
    }

    public bool IsSquareAttacked(Square target, PieceColor by)
    {
        // Pawns attack diagonally forward, so look one rank "behind" the target from the attacker's view.
        var pawnRank = by == PieceColor.White ? -1 : 1;
        foreach (var fileDelta in new[] { -1, 1 })
        {
            if (IsPiece(target.Offset(fileDelta, pawnRank), PieceType.Pawn, by)) return true;
        }

        foreach (var (f, r) in KnightJumps)
        {
            if (IsPiece(target.Offset(f, r), PieceType.Knight, by)) return true;
        }

        foreach (var (f, r) in KingSteps)
        {
            if (IsPiece(target.Offset(f, r), PieceType.King, by)) return true;
        }

        return IsAttackedBySlider(target, by, RookDirections, PieceType.Rook) ||
               IsAttackedBySlider(target, by, BishopDirections, PieceType.Bishop);
    }

    private bool IsAttackedBySlider(Square target, PieceColor by, (int File, int Rank)[] directions, PieceType slider)
    {
        foreach (var (f, r) in directions)
        {
            var square = target.Offset(f, r);
            while (square.IsOnBoard)
            {
                if (_board[square.Index] is { } piece)
                {
                    if (piece.Color == by && (piece.Type == slider || piece.Type == PieceType.Queen)) return true;
                    break;
                }
                square = square.Offset(f, r);
            }
        }
        return false;
    }

    private bool IsPiece(Square square, PieceType type, PieceColor color) =>
        square.IsOnBoard && _board[square.Index] is { } piece && piece.Type == type && piece.Color == color;

    private IEnumerable<Move> GeneratePseudoLegalMoves()
    {
        for (var i = 0; i < 64; i++)
        {
            if (_board[i] is not { } piece || piece.Color != SideToMove) continue;
            var from = Square.FromIndex(i);

            var moves = piece.Type switch
            {
                PieceType.Pawn => PawnMoves(from),
                PieceType.Knight => StepMoves(from, KnightJumps),
                PieceType.Bishop => SlideMoves(from, BishopDirections),
                PieceType.Rook => SlideMoves(from, RookDirections),
                PieceType.Queen => SlideMoves(from, RookDirections).Concat(SlideMoves(from, BishopDirections)),
                _ => StepMoves(from, KingSteps).Concat(CastlingMoves(from)),
            };
            foreach (var move in moves) yield return move;
        }
    }

    private IEnumerable<Move> PawnMoves(Square from)
    {
        var forward = SideToMove == PieceColor.White ? 1 : -1;
        var startRank = SideToMove == PieceColor.White ? 1 : 6;
        var lastRank = SideToMove == PieceColor.White ? 7 : 0;

        IEnumerable<Move> WithPromotions(Square to) => to.Rank == lastRank
            ? PromotionPieces.Select(p => new Move(from, to, p))
            : [new Move(from, to)];

        var oneStep = from.Offset(0, forward);
        if (oneStep.IsOnBoard && _board[oneStep.Index] is null)
        {
            foreach (var move in WithPromotions(oneStep)) yield return move;

            var twoSteps = from.Offset(0, 2 * forward);
            if (from.Rank == startRank && _board[twoSteps.Index] is null) yield return new Move(from, twoSteps);
        }

        foreach (var fileDelta in new[] { -1, 1 })
        {
            var to = from.Offset(fileDelta, forward);
            if (!to.IsOnBoard) continue;

            var isEnemy = _board[to.Index] is { } target && target.Color != SideToMove;
            if (isEnemy || to == EnPassant)
            {
                foreach (var move in WithPromotions(to)) yield return move;
            }
        }
    }

    private IEnumerable<Move> StepMoves(Square from, (int File, int Rank)[] steps)
    {
        foreach (var (f, r) in steps)
        {
            var to = from.Offset(f, r);
            if (to.IsOnBoard && _board[to.Index]?.Color != SideToMove) yield return new Move(from, to);
        }
    }

    private IEnumerable<Move> SlideMoves(Square from, (int File, int Rank)[] directions)
    {
        foreach (var (f, r) in directions)
        {
            var to = from.Offset(f, r);
            while (to.IsOnBoard)
            {
                if (_board[to.Index] is { } blocker)
                {
                    if (blocker.Color != SideToMove) yield return new Move(from, to);
                    break;
                }
                yield return new Move(from, to);
                to = to.Offset(f, r);
            }
        }
    }

    private IEnumerable<Move> CastlingMoves(Square from)
    {
        var white = SideToMove == PieceColor.White;
        var rank = white ? 0 : 7;
        if (from != new Square(4, rank)) yield break;

        var opponent = SideToMove.Opponent();
        if (IsSquareAttacked(from, opponent)) yield break;

        var rook = new Piece(PieceType.Rook, SideToMove);

        var kingSide = white ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
        if (Castling.HasFlag(kingSide) &&
            _board[new Square(7, rank).Index] == rook &&
            _board[new Square(5, rank).Index] is null &&
            _board[new Square(6, rank).Index] is null &&
            !IsSquareAttacked(new Square(5, rank), opponent))
        {
            // The destination square is checked by the general legality filter.
            yield return new Move(from, new Square(6, rank));
        }

        var queenSide = white ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;
        if (Castling.HasFlag(queenSide) &&
            _board[new Square(0, rank).Index] == rook &&
            _board[new Square(1, rank).Index] is null &&
            _board[new Square(2, rank).Index] is null &&
            _board[new Square(3, rank).Index] is null &&
            !IsSquareAttacked(new Square(3, rank), opponent))
        {
            yield return new Move(from, new Square(2, rank));
        }
    }
}
