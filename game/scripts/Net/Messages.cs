using System.Text.Json;
using System.Text.Json.Serialization;

namespace FpsChess.Net;

/// <summary>What a client asks for when creating a room.</summary>
public sealed class RoomSettings
{
    public string PlayerName { get; set; } = "Spieler";
    public bool VsComputer { get; set; }
    public int AiLevel { get; set; } = 2;

    /// <summary>"White", "Black" or "Random".</summary>
    public string HumanColor { get; set; } = "White";

    public long? InitialMs { get; set; }
    public long? IncrementMs { get; set; }

    /// <summary>A save file to continue (computer games only).</summary>
    public string? SaveJson { get; set; }

    public bool Public { get; set; } = true;
}

public sealed class PlayerInfo
{
    public string Name { get; set; } = "";
    public bool IsComputer { get; set; }
    public bool Connected { get; set; }
}

/// <summary>Full state of a room. Clients rebuild their local game from it by replaying the moves.</summary>
public sealed class MatchStateDto
{
    public string Code { get; set; } = "";
    public string Theme { get; set; } = "";
    public PlayerInfo White { get; set; } = new();
    public PlayerInfo Black { get; set; } = new();
    public bool VsComputer { get; set; }
    public int AiLevel { get; set; }
    public string StartFen { get; set; } = "";
    public List<string> Moves { get; set; } = [];
    public string? PendingMove { get; set; }
    public long? InitialMs { get; set; }
    public long? IncrementMs { get; set; }
    public long? WhiteMs { get; set; }
    public long? BlackMs { get; set; }

    /// <summary>Set when the game ended by resignation or time ("Resignation"/"TimeForfeit"); replay cannot know that.</summary>
    public string? ForfeitStatus { get; set; }
    public string? ForfeitedBy { get; set; }
}

public sealed class RoomListEntry
{
    public string Code { get; set; } = "";
    public string Host { get; set; } = "";
    public string TimeControl { get; set; } = "";
}

public sealed class DuelStartDto
{
    public string Move { get; set; } = "";
    public string AttackerSquare { get; set; } = "";
    public string DefenderSquare { get; set; } = "";
    public string Attacker { get; set; } = "";
    public string Defender { get; set; } = "";
    public string AttackerColor { get; set; } = "";
}

/// <summary>Indexes into the float array of a duel snapshot.</summary>
public static class SnapshotField
{
    public const int AttackerHealth = 0;
    public const int DefenderHealth = 1;
    public const int AttackerAmmo = 2;
    public const int DefenderAmmo = 3;
    public const int AttackerReload = 4;
    public const int DefenderReload = 5;
    public const int TimeLeft = 6;
    public const int CountdownLeft = 7;
    public const int Phase = 8;
    public const int AttackerGrenades = 9;
    public const int DefenderGrenades = 10;
    public const int AttackerDashCooldown = 11;
    public const int DefenderDashCooldown = 12;
    public const int AttackerSpinUp = 13;
    public const int DefenderSpinUp = 14;
    public const int IntroLeft = 15;
    public const int Count = 16;
}

public static class NetJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <exception cref="InvalidDataException">The text is not a valid message of this type.</exception>
    public static T Read<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("Empty message.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"Malformed {typeof(T).Name}: {e.Message}", e);
        }
    }
}
