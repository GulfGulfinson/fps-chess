namespace Chess.Core;

public enum GameStatus
{
    Ongoing,
    Checkmate,
    Stalemate,
    ThreefoldRepetition,
    FiftyMoveRule,
    InsufficientMaterial,
    TimeForfeit,
    Resignation,

    /// <summary>Duel variant only: an exposed king was captured and lost its duel.</summary>
    KingCaptured,
}
