using Chess.Core;
using Godot;

namespace FpsChess.Arena;

/// <summary>
/// World layout at piece scale: a square is 3 m, the board top and the table top are y = 0,
/// white sits at +Z and files run along +X.
/// </summary>
public static class BoardGeometry
{
    public const float Tile = 3f;
    public const float HalfBoard = Tile * 4f;

    /// <summary>Half the table's edge length; beyond it a fighter falls.</summary>
    public const float HalfTable = 30f;

    /// <summary>A fighter below this height has fallen off the table and loses the duel.</summary>
    public const float FallHeight = -5f;

    public const uint WorldLayer = 1;
    public const uint PieceLayer = 2;
    public const uint PlayerLayer = 4;
    public const uint HitboxLayer = 8;

    public static Vector3 Center(Square square) => new((square.File - 3.5f) * Tile, 0f, (3.5f - square.Rank) * Tile);

    public static Square? SquareAt(Vector3 point)
    {
        var square = new Square(Mathf.FloorToInt(point.X / Tile + 4f), Mathf.FloorToInt(4f - point.Z / Tile));
        return square.IsOnBoard ? square : null;
    }

    /// <summary>Rotation (around Y) that makes something at <paramref name="from"/> face <paramref name="to"/>.</summary>
    public static float YawTowards(Vector3 from, Vector3 to)
    {
        var direction = to - from;
        return Mathf.Atan2(-direction.X, -direction.Z);
    }
}
