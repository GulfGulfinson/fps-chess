namespace FpsChess.Net;

public static class NetConfig
{
    /// <summary>Bump whenever a message changes; client and server must match exactly.</summary>
    public const int ProtocolVersion = 1;

    public const int DefaultPort = 11800;

    /// <summary>How often the server sends duel snapshots and clients send their pose.</summary>
    public const float SyncInterval = 1f / 30f;

    /// <summary>Time for the camera to fly into the fighter before the duel countdown starts.</summary>
    public const float DuelIntroSeconds = 1.2f;

    /// <summary>How far a reported shot origin may be from the server's last known eye position.</summary>
    public const float MaxShotOriginError = 1.5f;

    /// <summary>Below this height a fighter has fallen off the table.</summary>
    public const float FallHeight = -5f;
}
