using Godot;

namespace FpsChess.Arena;

public enum ArenaThemeId
{
    Library,
    CastleHall,
    Lab,
}

/// <summary>Look of a themed room. The obstacles themselves are built in <see cref="ArenaBuilder"/>.</summary>
public sealed record ArenaTheme(
    ArenaThemeId Id,
    string Name,
    Color Table,
    Color Walls,
    Color Floor,
    Color Ambient,
    Color KeyLight,
    float KeyEnergy,
    Color LampLight)
{
    public static ArenaTheme Get(ArenaThemeId id) => id switch
    {
        ArenaThemeId.Library => new(id, "Bibliothek", new Color("5a3a22"), new Color("3b2a1f"), new Color("2a1d14"),
            new Color("6b5440"), new Color("ffd9a8"), 1.1f, new Color("ffb366")),
        ArenaThemeId.CastleHall => new(id, "Burgsaal", new Color("4a3b30"), new Color("44454a"), new Color("2c2c30"),
            new Color("4f5260"), new Color("ffe2c0"), 1.0f, new Color("ff8a3d")),
        _ => new(id, "Labor", new Color("8f9aa6"), new Color("9fb0c0"), new Color("5d6874"),
            new Color("5f7488"), new Color("e8f4ff"), 0.85f, new Color("6fd3ff")),
    };

    public static ArenaThemeId Random() => (ArenaThemeId)System.Random.Shared.Next(3);
}
