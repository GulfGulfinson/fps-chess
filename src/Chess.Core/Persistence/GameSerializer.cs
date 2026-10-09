using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chess.Core.Persistence;

/// <summary>
/// Reads and writes save files as JSON. The file stores the start position and the move list,
/// and loading replays every move through <see cref="Game"/>, so an edited file can never
/// produce an illegal position.
/// </summary>
public static class GameSerializer
{
    public const int CurrentVersion = 2;
    private const string AttackerWonMarker = ":A";
    private const string DefenderWonMarker = ":D";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SavedGame save)
    {
        var game = save.Game;
        if (game.PendingDuel is not null) throw new InvalidOperationException("Cannot save while a duel is being fought.");

        var dto = new SaveFileDto
        {
            Version = CurrentVersion,
            SavedAt = DateTimeOffset.Now,
            Variant = game.Variant.ToString(),
            StartFen = game.StartFen,
            Moves = game.Moves.Select((m, i) => m.ToUci() + game.DuelOutcomes[i] switch
            {
                DuelOutcome.AttackerWon => AttackerWonMarker,
                DuelOutcome.DefenderWon => DefenderWonMarker,
                _ => "",
            }).ToList(),
            White = save.Setup.White.ToString(),
            Black = save.Setup.Black.ToString(),
            AiLevel = save.Setup.AiLevel,
        };

        if (save.Setup.TimeControl is { } tc)
        {
            dto.InitialMs = (long)tc.Initial.TotalMilliseconds;
            dto.IncrementMs = (long)tc.Increment.TotalMilliseconds;
        }
        if (save.Clock is { } clock)
        {
            dto.WhiteMs = (long)clock.Remaining(PieceColor.White).TotalMilliseconds;
            dto.BlackMs = (long)clock.Remaining(PieceColor.Black).TotalMilliseconds;
        }
        if (game.ForfeitedBy is { } loser)
        {
            dto.EndedBy = game.Status.ToString();
            dto.Loser = loser.ToString();
        }

        return JsonSerializer.Serialize(dto, Options);
    }

    public static SavedGame Deserialize(string json)
    {
        SaveFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SaveFileDto>(json, Options);
        }
        catch (JsonException e)
        {
            throw new InvalidSaveFileException("The save file is not valid JSON.", e);
        }

        if (dto is null) throw new InvalidSaveFileException("The save file is empty.");
        if (dto.Version is < 1 or > CurrentVersion)
            throw new InvalidSaveFileException($"Unsupported save file version {dto.Version}.");

        var variant = dto.Variant is null ? GameVariant.Classic : ParseEnum<GameVariant>(dto.Variant, "variant");
        var setup = new GameSetup(
            ParseEnum<PlayerKind>(dto.White, "white"),
            ParseEnum<PlayerKind>(dto.Black, "black"),
            dto.AiLevel,
            dto is { InitialMs: { } initial, IncrementMs: { } increment }
                ? new TimeControl(TimeSpan.FromMilliseconds(initial), TimeSpan.FromMilliseconds(increment))
                : null,
            variant);
        if (setup.AiLevel is < GameSetup.MinAiLevel or > GameSetup.MaxAiLevel)
            throw new InvalidSaveFileException($"AI level {setup.AiLevel} is out of range.");

        var game = ReplayGame(dto, variant);
        var clock = RestoreClock(setup, dto);

        if (dto.EndedBy is not null)
        {
            var loser = ParseEnum<PieceColor>(dto.Loser, "loser");
            switch (ParseEnum<GameStatus>(dto.EndedBy, "endedBy"))
            {
                case GameStatus.Resignation: game.Resign(loser); break;
                case GameStatus.TimeForfeit: game.ForfeitOnTime(loser); break;
                default: throw new InvalidSaveFileException($"Unsupported result '{dto.EndedBy}'.");
            }
        }

        return new SavedGame(setup, game, clock);
    }

    public static void SaveToFile(SavedGame save, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(save));
    }

    public static SavedGame LoadFromFile(string path) => Deserialize(File.ReadAllText(path));

    private static Game ReplayGame(SaveFileDto dto, GameVariant variant)
    {
        if (dto.StartFen is null || dto.Moves is null)
            throw new InvalidSaveFileException("The save file is missing the start position or the moves.");

        Game game;
        try
        {
            game = new Game(dto.StartFen, variant);
        }
        catch (FormatException e)
        {
            throw new InvalidSaveFileException("The start position in the save file is invalid.", e);
        }

        for (var i = 0; i < dto.Moves.Count; i++)
        {
            var text = dto.Moves[i];
            var (uci, outcome) = text.Length > 2 && text[^2] == ':'
                ? (text[..^2], text[^2..] switch
                {
                    AttackerWonMarker => DuelOutcome.AttackerWon,
                    DefenderWonMarker => DuelOutcome.DefenderWon,
                    _ => throw new InvalidSaveFileException($"Move {i + 1} '{text}' has an unknown duel result."),
                })
                : (text, (DuelOutcome?)null);

            Move move;
            try
            {
                move = Move.ParseUci(uci);
            }
            catch (FormatException e)
            {
                throw new InvalidSaveFileException($"Move {i + 1} '{text}' is malformed.", e);
            }
            if (!game.TryMakeMove(move))
                throw new InvalidSaveFileException($"Move {i + 1} '{text}' is illegal.");

            // A duel result is required exactly when the move started a duel.
            if ((game.PendingDuel is null) != (outcome is null))
                throw new InvalidSaveFileException($"Move {i + 1} '{text}' does not match its duel result.");
            if (outcome is { } result) game.ResolveDuel(result);
        }
        return game;
    }

    private static ChessClock? RestoreClock(GameSetup setup, SaveFileDto dto)
    {
        if (setup.TimeControl is not { } tc) return null;
        if (dto.WhiteMs is not { } white || dto.BlackMs is not { } black)
            throw new InvalidSaveFileException("A timed game must store both clocks.");
        try
        {
            return ChessClock.Restore(tc.Initial, tc.Increment, TimeSpan.FromMilliseconds(white), TimeSpan.FromMilliseconds(black));
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new InvalidSaveFileException("The clock values in the save file are invalid.", e);
        }
    }

    private static T ParseEnum<T>(string? value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidSaveFileException($"Invalid value '{value}' for {field}.");

    private sealed class SaveFileDto
    {
        public int Version { get; set; }
        public DateTimeOffset SavedAt { get; set; }
        public string? Variant { get; set; }
        public string? StartFen { get; set; }
        public List<string>? Moves { get; set; }
        public string? White { get; set; }
        public string? Black { get; set; }
        public int AiLevel { get; set; }
        public long? InitialMs { get; set; }
        public long? IncrementMs { get; set; }
        public long? WhiteMs { get; set; }
        public long? BlackMs { get; set; }
        public string? EndedBy { get; set; }
        public string? Loser { get; set; }
    }
}
