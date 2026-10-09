namespace Chess.Core;

public enum GameVariant
{
    /// <summary>Standard chess: captures happen immediately.</summary>
    Classic,

    /// <summary>FPS Chess: every capture is decided by a duel between the two pieces.</summary>
    Duel,
}

public enum DuelOutcome
{
    AttackerWon,
    DefenderWon,
}

/// <summary>A capture waiting for its duel. The defender square differs from the target for en passant.</summary>
public sealed record PendingDuel(Move Move, Piece Attacker, Piece Defender, Square AttackerSquare, Square DefenderSquare)
{
    public static PendingDuel Describe(Position position, Move move)
    {
        var attacker = position.PieceAt(move.From) ?? throw new ArgumentException($"No piece on {move.From}.", nameof(move));
        var defenderSquare = position.PieceAt(move.To) is null ? new Square(move.To.File, move.From.Rank) : move.To;
        var defender = position.PieceAt(defenderSquare) ?? throw new ArgumentException($"{move} is not a capture.", nameof(move));
        return new PendingDuel(move, attacker, defender, move.From, defenderSquare);
    }
}
