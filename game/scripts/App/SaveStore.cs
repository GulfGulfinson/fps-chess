using Chess.Core;
using Chess.Core.Persistence;
using FpsChess.Ui;
using Godot;

namespace FpsChess.App;

/// <summary>Save files live as JSON in the Godot user directory (user://saves).</summary>
public static class SaveStore
{
    public sealed record Entry(string Path, string Name, DateTime Modified);

    public static string DirectoryPath => ProjectSettings.GlobalizePath("user://saves");

    /// <summary>Writes the game to a new file named after the current time and returns that name.</summary>
    public static string Save(SavedGame save)
    {
        var path = NewPath();
        GameSerializer.SaveToFile(save, path);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>Writes an already serialized save (sent by the match server) and returns its name.</summary>
    public static string SaveJson(string json)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = NewPath();
        File.WriteAllText(path, json);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public static string ReadJson(Entry entry) => File.ReadAllText(entry.Path);

    private static string NewPath()
    {
        var name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = System.IO.Path.Combine(DirectoryPath, name + ".json");
        for (var i = 2; File.Exists(path); i++) path = System.IO.Path.Combine(DirectoryPath, $"{name}_{i}.json");
        return path;
    }

    public static IReadOnlyList<Entry> List()
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        return Directory.GetFiles(DirectoryPath, "*.json")
            .Select(path => new Entry(path, System.IO.Path.GetFileNameWithoutExtension(path), File.GetLastWriteTime(path)))
            .OrderByDescending(e => e.Modified)
            .ToList();
    }

    /// <exception cref="InvalidSaveFileException">The file is corrupt or contains illegal moves.</exception>
    public static SavedGame Load(Entry entry) => GameSerializer.LoadFromFile(entry.Path);

    public static void Delete(Entry entry) => File.Delete(entry.Path);

    /// <summary>One-line summary for the load list; corrupt files are listed so the user can delete them.</summary>
    public static string Describe(Entry entry)
    {
        var date = entry.Modified.ToString("dd.MM.yyyy HH:mm");
        try
        {
            var save = Load(entry);
            var players = $"{Texts.PlayerShort(save.Setup.White)} gegen {Texts.PlayerShort(save.Setup.Black)}";
            var state = save.Game.IsOver ? "beendet" : $"Zug {save.Game.Position.FullmoveNumber}";
            var clock = save.Clock is null ? "" : ", mit Uhr";
            var mode = save.Setup.Variant == GameVariant.Duel ? "FPS Chess" : "2D";
            return $"{date}   {mode}: {players}, {state}{clock}";
        }
        catch (Exception e) when (e is InvalidSaveFileException or IOException)
        {
            return $"{date}   (beschädigt: {e.Message})";
        }
    }
}
