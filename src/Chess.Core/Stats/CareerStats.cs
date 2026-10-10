using System.Text.Json;

namespace Chess.Core.Stats;

public enum GameResultKind
{
    Won,
    Lost,
    Draw,
}

/// <summary>A player's long-term record across games and duels, stored as JSON in the user settings.</summary>
public sealed class CareerStats
{
    // A piece needs this many duels before its win rate counts as a record rather than luck.
    private const int MinimumDuelsForBest = 3;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public int Games { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int GamesDrawn { get; set; }
    public int Duels { get; set; }
    public int DuelsWon { get; set; }
    public int Headshots { get; set; }
    public float DamageDealt { get; set; }

    /// <summary>Per piece type: duels fought and duels won.</summary>
    public Dictionary<PieceType, int[]> ByPiece { get; set; } = [];

    public float DuelWinRate => Duels == 0 ? 0f : DuelsWon / (float)Duels;

    /// <summary>The piece this player fought with most often.</summary>
    public PieceType? FavouritePiece =>
        ByPiece.Count == 0 ? null : ByPiece.MaxBy(p => p.Value[0]).Key;

    /// <summary>The piece with the best duel win rate among those used often enough.</summary>
    public PieceType? BestPiece
    {
        get
        {
            var candidates = ByPiece.Where(p => p.Value[0] >= MinimumDuelsForBest).ToList();
            return candidates.Count == 0 ? null : candidates.MaxBy(p => p.Value[1] / (float)p.Value[0]).Key;
        }
    }

    public void RecordDuel(PieceType piece, bool won, int headshots, float damageDealt)
    {
        Duels++;
        if (won) DuelsWon++;
        Headshots += headshots;
        DamageDealt += damageDealt;
        if (!ByPiece.TryGetValue(piece, out var record)) ByPiece[piece] = record = new int[2];
        record[0]++;
        if (won) record[1]++;
    }

    public void RecordGame(GameResultKind result)
    {
        Games++;
        switch (result)
        {
            case GameResultKind.Won: GamesWon++; break;
            case GameResultKind.Lost: GamesLost++; break;
            default: GamesDrawn++; break;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads stored stats; anything unreadable starts a fresh record instead of failing the game.</summary>
    public static CareerStats FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CareerStats();
        try
        {
            var stats = JsonSerializer.Deserialize<CareerStats>(json, Options) ?? new CareerStats();
            // Drop malformed per-piece entries rather than crashing on them later.
            stats.ByPiece = stats.ByPiece.Where(p => p.Value is { Length: 2 }).ToDictionary(p => p.Key, p => p.Value);
            return stats;
        }
        catch (JsonException)
        {
            return new CareerStats();
        }
    }
}
