using FpsChess.Net;
using Godot;

namespace FpsChess.App;

/// <summary>Small per-user conveniences (player name, last server) in user://settings.cfg.</summary>
public static class UserPrefs
{
    private const string Path = "user://settings.cfg";
    private const string Section = "online";

    public static string PlayerName
    {
        get => Read("name", "Spieler");
        set => Write("name", value);
    }

    public static string ServerUrl
    {
        get => Read("server", NetConfig.DefaultServerUrl);
        set => Write("server", value);
    }

    public static GraphicsLevel Graphics
    {
        get => Enum.TryParse<GraphicsLevel>(Read("graphics", nameof(GraphicsLevel.Medium)), out var level) ? level : GraphicsLevel.Medium;
        set => Write("graphics", value.ToString());
    }

    private static string Read(string key, string fallback)
    {
        var config = new ConfigFile();
        // A missing file just means nothing was saved yet.
        return config.Load(Path) == Error.Ok ? config.GetValue(Section, key, fallback).AsString() : fallback;
    }

    private static void Write(string key, string value)
    {
        var config = new ConfigFile();
        config.Load(Path);
        config.SetValue(Section, key, value);
        var error = config.Save(Path);
        if (error != Error.Ok) GD.PushWarning($"Could not save settings: {error}");
    }
}
