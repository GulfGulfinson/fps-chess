namespace FpsChess.Net;

public static class NetConfig
{
    /// <summary>Bump whenever a message changes; client and server must match exactly.</summary>
    public const int ProtocolVersion = 2;

    public const int DefaultPort = 11800;

    /// <summary>The public match server on gulfinson.</summary>
    public const string DefaultServerUrl = "wss://chess.gulfgulfinson.de";

    /// <summary>How long a seat is kept for a player whose connection dropped.</summary>
    public const double ReconnectSeconds = 60;

    /// <summary>Well below Cloudflare's 100 s idle timeout for WebSockets.</summary>
    public const double KeepaliveSeconds = 20;

    /// <summary>How often the server sends duel snapshots and clients send their pose.</summary>
    public const float SyncInterval = 1f / 30f;

    /// <summary>
    /// Time between a capture and the duel countdown: the board shows who attacks whom, a versus card
    /// introduces both fighters, then the camera flies into the player's piece.
    /// </summary>
    public const float DuelIntroSeconds = 4.2f;

    /// <summary>How far a reported shot origin may be from the server's last known eye position.</summary>
    public const float MaxShotOriginError = 2f;

    /// <summary>How far back the server looks when checking a hit the shooter saw (covers lag and smoothing).</summary>
    public const float HitHistorySeconds = 0.6f;

    /// <summary>Extra radius around a fighter's body within which a claimed hit is accepted.</summary>
    public const float HitTolerance = 0.35f;

    /// <summary>Below this height a fighter has fallen off the table.</summary>
    public const float FallHeight = -5f;
}
