namespace Chess.Core;

public enum PlayerKind
{
    Human,
    Computer,
}

public sealed record TimeControl(TimeSpan Initial, TimeSpan Increment);

/// <summary>
/// Who plays each side, how strong the computer is (search depth in plies), the optional clock
/// and whether captures are decided by duels.
/// </summary>
public sealed record GameSetup(
    PlayerKind White,
    PlayerKind Black,
    int AiLevel = 2,
    TimeControl? TimeControl = null,
    GameVariant Variant = GameVariant.Classic)
{
    public const int MinAiLevel = 1;
    public const int MaxAiLevel = 4;

    public PlayerKind PlayerFor(PieceColor color) => color == PieceColor.White ? White : Black;
}
