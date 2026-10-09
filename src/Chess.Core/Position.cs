using System.Text;

namespace Chess.Core;

/// <summary>
/// An immutable chess position: piece placement plus side to move, castling rights,
/// en passant square and move counters. Applying a move returns a new position.
/// </summary>
public sealed partial class Position
{
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly Piece?[] _board;

    public PieceColor SideToMove { get; }
    public CastlingRights Castling { get; }
    public Square? EnPassant { get; }
    public int HalfmoveClock { get; }
    public int FullmoveNumber { get; }

    private Position(Piece?[] board, PieceColor sideToMove, CastlingRights castling, Square? enPassant,
        int halfmoveClock, int fullmoveNumber)
    {
        _board = board;
        SideToMove = sideToMove;
        Castling = castling;
        EnPassant = enPassant;
        HalfmoveClock = halfmoveClock;
        FullmoveNumber = fullmoveNumber;
    }

    // Lazy because static field initializers across partial files run in unspecified order.
    private static readonly Lazy<Position> LazyStart = new(() => FromFen(StartFen));

    public static Position Start => LazyStart.Value;

    public Piece? PieceAt(Square square) => _board[square.Index];

    public IEnumerable<(Square Square, Piece Piece)> Pieces()
    {
        for (var i = 0; i < 64; i++)
        {
            if (_board[i] is { } piece) yield return (Square.FromIndex(i), piece);
        }
    }

    public Square KingSquare(PieceColor color) =>
        TryKingSquare(color) ?? throw new InvalidOperationException($"No {color} king on the board.");

    /// <summary>The king's square, or null once it has been captured (duel variant only).</summary>
    public Square? TryKingSquare(PieceColor color)
    {
        for (var i = 0; i < 64; i++)
        {
            if (_board[i] is { Type: PieceType.King } piece && piece.Color == color) return Square.FromIndex(i);
        }
        return null;
    }

    public bool IsInCheck(PieceColor color) =>
        TryKingSquare(color) is { } king && IsSquareAttacked(king, color.Opponent());

    /// <param name="allowExposedKing">
    /// Accept positions where the side not to move is in check. They are illegal in classic chess but
    /// arise in the duel variant after a lost capture.
    /// </param>
    public static Position FromFen(string fen, bool allowExposedKing = false)
    {
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) throw new FormatException($"FEN must have 6 fields: '{fen}'.");

        var board = ParsePlacement(parts[0]);

        var side = parts[1] switch
        {
            "w" => PieceColor.White,
            "b" => PieceColor.Black,
            _ => throw new FormatException($"Invalid side to move '{parts[1]}'."),
        };

        var castling = CastlingRights.None;
        if (parts[2] != "-")
        {
            foreach (var c in parts[2])
            {
                castling |= c switch
                {
                    'K' => CastlingRights.WhiteKingSide,
                    'Q' => CastlingRights.WhiteQueenSide,
                    'k' => CastlingRights.BlackKingSide,
                    'q' => CastlingRights.BlackQueenSide,
                    _ => throw new FormatException($"Invalid castling rights '{parts[2]}'."),
                };
            }
        }

        Square? enPassant = parts[3] == "-" ? null : Square.Parse(parts[3]);

        if (!int.TryParse(parts[4], out var halfmove) || halfmove < 0)
            throw new FormatException($"Invalid halfmove clock '{parts[4]}'.");
        if (!int.TryParse(parts[5], out var fullmove) || fullmove < 1)
            throw new FormatException($"Invalid fullmove number '{parts[5]}'.");

        var position = new Position(board, side, castling, enPassant, halfmove, fullmove);
        position.Validate(allowExposedKing);
        return position;
    }

    private static Piece?[] ParsePlacement(string placement)
    {
        var rows = placement.Split('/');
        if (rows.Length != 8) throw new FormatException($"Piece placement must have 8 ranks: '{placement}'.");

        var board = new Piece?[64];
        for (var row = 0; row < 8; row++)
        {
            var rank = 7 - row;
            var file = 0;
            foreach (var c in rows[row])
            {
                if (c is >= '1' and <= '8')
                {
                    file += c - '0';
                    continue;
                }
                var piece = Piece.FromFenChar(c) ?? throw new FormatException($"Invalid piece '{c}' in FEN.");
                if (file > 7) throw new FormatException($"Rank {rank + 1} has too many squares.");
                board[new Square(file, rank).Index] = piece;
                file++;
            }
            if (file != 8) throw new FormatException($"Rank {rank + 1} must describe 8 squares.");
        }
        return board;
    }

    private void Validate(bool allowExposedKing)
    {
        foreach (var color in new[] { PieceColor.White, PieceColor.Black })
        {
            var kings = Pieces().Count(p => p.Piece == new Piece(PieceType.King, color));
            if (kings != 1) throw new FormatException($"{color} must have exactly one king, found {kings}.");
        }
        if (Pieces().Any(p => p.Piece.Type == PieceType.Pawn && p.Square.Rank is 0 or 7))
            throw new FormatException("Pawns cannot stand on the first or last rank.");
        if (!allowExposedKing && IsInCheck(SideToMove.Opponent()))
            throw new FormatException("The side not to move is in check.");
    }

    public string ToFen()
    {
        var sb = new StringBuilder();
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                if (_board[new Square(file, rank).Index] is { } piece)
                {
                    if (empty > 0) sb.Append(empty);
                    empty = 0;
                    sb.Append(piece.ToFenChar());
                }
                else
                {
                    empty++;
                }
            }
            if (empty > 0) sb.Append(empty);
            if (rank > 0) sb.Append('/');
        }

        sb.Append(SideToMove == PieceColor.White ? " w " : " b ");

        if (Castling == CastlingRights.None)
        {
            sb.Append('-');
        }
        else
        {
            if (Castling.HasFlag(CastlingRights.WhiteKingSide)) sb.Append('K');
            if (Castling.HasFlag(CastlingRights.WhiteQueenSide)) sb.Append('Q');
            if (Castling.HasFlag(CastlingRights.BlackKingSide)) sb.Append('k');
            if (Castling.HasFlag(CastlingRights.BlackQueenSide)) sb.Append('q');
        }

        sb.Append(' ').Append(EnPassant?.ToString() ?? "-");
        sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
        return sb.ToString();
    }

    /// <summary>
    /// Identity of the position for threefold repetition: placement, side, castling, and the
    /// en passant square only when an en passant capture is actually legal.
    /// </summary>
    public string RepetitionKey()
    {
        var fields = ToFen().Split(' ');
        var enPassantMatters = EnPassant is { } ep && GenerateLegalMoves().Any(m =>
            m.To == ep && _board[m.From.Index]?.Type == PieceType.Pawn);
        return $"{fields[0]} {fields[1]} {fields[2]} {(enPassantMatters ? fields[3] : "-")}";
    }

    /// <summary>Applies a move without checking legality. Callers pass moves from <see cref="GenerateLegalMoves"/>.</summary>
    public Position Apply(Move move)
    {
        var board = (Piece?[])_board.Clone();
        var mover = board[move.From.Index] ?? throw new InvalidOperationException($"No piece on {move.From}.");
        var captured = board[move.To.Index];

        board[move.From.Index] = null;
        board[move.To.Index] = move.Promotion is { } promotion ? new Piece(promotion, mover.Color) : mover;

        if (mover.Type == PieceType.Pawn && move.To == EnPassant && captured is null)
        {
            board[new Square(move.To.File, move.From.Rank).Index] = null;
            captured = new Piece(PieceType.Pawn, mover.Color.Opponent());
        }

        if (mover.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2)
        {
            var kingSide = move.To.File == 6;
            var rookFrom = new Square(kingSide ? 7 : 0, move.From.Rank);
            var rookTo = new Square(kingSide ? 5 : 3, move.From.Rank);
            board[rookTo.Index] = board[rookFrom.Index];
            board[rookFrom.Index] = null;
        }

        var castling = Castling & ~(CastlingLostBy(move.From) | CastlingLostBy(move.To));

        Square? enPassant = null;
        if (mover.Type == PieceType.Pawn && Math.Abs(move.To.Rank - move.From.Rank) == 2)
            enPassant = new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2);

        var halfmove = mover.Type == PieceType.Pawn || captured is not null ? 0 : HalfmoveClock + 1;
        var fullmove = SideToMove == PieceColor.Black ? FullmoveNumber + 1 : FullmoveNumber;

        return new Position(board, SideToMove.Opponent(), castling, enPassant, halfmove, fullmove);
    }

    /// <summary>
    /// Duel variant: the capturing piece lost its duel. It disappears from its square, the defender stays,
    /// and the turn passes as if a move had been played.
    /// </summary>
    public Position ApplyFailedCapture(Move move)
    {
        if (_board[move.From.Index] is null) throw new InvalidOperationException($"No piece on {move.From}.");
        var board = (Piece?[])_board.Clone();
        board[move.From.Index] = null;
        var fullmove = SideToMove == PieceColor.Black ? FullmoveNumber + 1 : FullmoveNumber;
        return new Position(board, SideToMove.Opponent(), Castling & ~CastlingLostBy(move.From), null, 0, fullmove);
    }

    private static CastlingRights CastlingLostBy(Square square) => square.Index switch
    {
        4 => CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide, // e1
        0 => CastlingRights.WhiteQueenSide, // a1
        7 => CastlingRights.WhiteKingSide, // h1
        60 => CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide, // e8
        56 => CastlingRights.BlackQueenSide, // a8
        63 => CastlingRights.BlackKingSide, // h8
        _ => CastlingRights.None,
    };

    public bool IsCapture(Move move) =>
        _board[move.To.Index] is not null ||
        (_board[move.From.Index]?.Type == PieceType.Pawn && move.To == EnPassant);
}
