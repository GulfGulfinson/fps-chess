namespace FpsChess.Net;

/// <summary>The fixed quick messages players can send each other (keys 1 to 4).</summary>
public static class QuickChat
{
    public static readonly string[] Messages = ["Hallo!", "Gut gespielt!", "Glück gehabt!", "Revanche?"];

    public static string? Text(int message) => message >= 0 && message < Messages.Length ? Messages[message] : null;
}
