using Chess.Core.Duels;
using Godot;

namespace FpsChess.Duel;

/// <summary>Spread and eye heights shared by the player's weapon, the server and the bot.</summary>
public static class Ballistics
{
    public static float EyeHeight(float pieceHeight) => pieceHeight * 0.85f;

    public static float ChestHeight(float pieceHeight) => pieceHeight * 0.6f;

    /// <summary>A random direction inside a cone of <paramref name="coneDegrees"/> around <paramref name="direction"/>.</summary>
    public static Vector3 Scatter(Vector3 direction, float coneDegrees)
    {
        direction = direction.Normalized();
        if (coneDegrees <= 0f) return direction;
        var side = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : direction.Cross(Vector3.Up).Normalized();
        var up = side.Cross(direction).Normalized();
        // Uniform over the disc so the centre is not over-weighted.
        var radius = Mathf.Tan(Mathf.DegToRad(coneDegrees)) * Mathf.Sqrt(GD.Randf());
        var angle = GD.Randf() * Mathf.Tau;
        return (direction + side * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius).Normalized();
    }

    public static Vector3[] ShotDirections(Vector3 aim, WeaponStats weapon, bool zoomed, float extraErrorDegrees = 0f)
    {
        var spread = (zoomed && weapon.ZoomedSpreadDegrees is { } zoomedSpread ? zoomedSpread : weapon.SpreadDegrees) + extraErrorDegrees;
        var directions = new Vector3[weapon.Pellets];
        for (var i = 0; i < directions.Length; i++) directions[i] = Scatter(aim, spread);
        return directions;
    }
}
