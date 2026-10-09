namespace Chess.Core.Persistence;

/// <summary>Everything needed to resume a game. <see cref="Clock"/> is null for untimed games.</summary>
public sealed record SavedGame(GameSetup Setup, Game Game, ChessClock? Clock);
