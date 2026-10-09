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
}
