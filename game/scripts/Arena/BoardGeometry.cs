using Chess.Core;
using Godot;

namespace FpsChess.Fps;

/// <summary>Maps chess squares to world space. White sits at +Z, files run along +X, the board top is y = 0.</summary>
public static class BoardGeometry
{
    public const float Tile = 3f;
    public const float HalfBoard = Tile * 4f;

    public static Vector3 Center(Square square) => new((square.File - 3.5f) * Tile, 0f, (3.5f - square.Rank) * Tile);

    public static Square? SquareAt(Vector3 point)
    {
        var square = new Square(Mathf.FloorToInt(point.X / Tile + 4f), Mathf.FloorToInt(4f - point.Z / Tile));
        return square.IsOnBoard ? square : null;
    }

    /// <summary>Where a player of this colour stands to look at the board from their side.</summary>
    public static Transform3D SpawnFor(PieceColor color)
    {
        var z = HalfBoard + 6f;
        return color == PieceColor.White
            ? new Transform3D(Basis.Identity, new Vector3(0, 0.1f, z))
            : new Transform3D(new Basis(Vector3.Up, Mathf.Pi), new Vector3(0, 0.1f, -z));
    }
}
