using System.Globalization;
using FpsChess.Net;
using Godot;

namespace FpsChess.App;

/// <summary>
/// Per-user settings in user://settings.cfg. The file is read once and kept in memory, because some
/// values (mouse sensitivity) are read on every mouse movement; every change is written straight back.
/// </summary>
public static class UserPrefs
{
    private const string Path = "user://settings.cfg";
    private const string Section = "settings";

    private static ConfigFile? _config;

    private static ConfigFile Config
    {
        get
        {
            if (_config is not null) return _config;
            _config = new ConfigFile();
            // A missing file just means nothing was saved yet.
            _config.Load(Path);
            return _config;
        }
    }

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

    /// <summary>Multiplier on the base mouse sensitivity, 0.2 to 3.</summary>
    public static float MouseSensitivity
    {
        get => Math.Clamp(ReadFloat("mouse", 1f), 0.2f, 3f);
        set => WriteFloat("mouse", value);
    }

    /// <summary>Vertical field of view in degrees for the first-person camera, 65 to 100.</summary>
    public static float FieldOfView
    {
        get => Math.Clamp(ReadFloat("fov", 80f), 65f, 100f);
        set => WriteFloat("fov", value);
    }

    /// <summary>Master volume, 0 to 1.</summary>
    public static float Volume
    {
        get => Math.Clamp(ReadFloat("volume", 0.8f), 0f, 1f);
        set => WriteFloat("volume", value);
    }

    public static bool Muted
    {
        get => Read("muted", "false") == "true";
        set => Write("muted", value ? "true" : "false");
    }

    /// <summary>Applies the volume settings to the master bus.</summary>
    public static void ApplyAudio()
    {
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Math.Max(Volume, 0.0001f)));
        AudioServer.SetBusMute(0, Muted || Volume <= 0.001f);
    }

    private static float ReadFloat(string key, float fallback) =>
        float.TryParse(Read(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static void WriteFloat(string key, float value) => Write(key, value.ToString("0.###", CultureInfo.InvariantCulture));

    private static string Read(string key, string fallback) =>
        Config.HasSectionKey(Section, key) ? Config.GetValue(Section, key).AsString() : LegacyRead(key) ?? fallback;

    // Name and server were stored in an "online" section before the settings panel existed.
    private static string? LegacyRead(string key) =>
        Config.HasSectionKey("online", key) ? Config.GetValue("online", key).AsString() : null;

    private static void Write(string key, string value)
    {
        Config.SetValue(Section, key, value);
        var error = Config.Save(Path);
        if (error != Error.Ok) GD.PushWarning($"Could not save settings: {error}");
    }
}
