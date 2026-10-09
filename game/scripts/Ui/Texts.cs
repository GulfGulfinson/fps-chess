using Chess.Core;

namespace FpsChess.Ui;

/// <summary>All German user-facing wording in one place.</summary>
public static class Texts
{
    public static string Color(PieceColor color) => color == PieceColor.White ? "Weiß" : "Schwarz";

    public static string Piece(PieceType type) => type switch
    {
        PieceType.Pawn => "Bauer",
        PieceType.Knight => "Springer",
        PieceType.Bishop => "Läufer",
        PieceType.Rook => "Turm",
        PieceType.Queen => "Dame",
        _ => "König",
    };

    public static string PlayerShort(PlayerKind kind) => kind == PlayerKind.Human ? "Mensch" : "Computer";

    public static string Player(GameSetup setup, PieceColor color) =>
        setup.PlayerFor(color) == PlayerKind.Human ? "Mensch" : $"Computer (Stufe {setup.AiLevel})";

    public static string AiLevel(int level) => level switch
    {
        1 => "Leicht (1 Halbzug)",
        2 => "Mittel (2 Halbzüge)",
        3 => "Stark (3 Halbzüge)",
        _ => "Sehr stark (4 Halbzüge)",
    };

    /// <summary>Unicode chess glyph; the filled set is used for both colours and tinted when drawn.</summary>
    public static string Glyph(PieceType type) => type switch
    {
        PieceType.Pawn => "♟",
        PieceType.Knight => "♞",
        PieceType.Bishop => "♝",
        PieceType.Rook => "♜",
        PieceType.Queen => "♛",
        _ => "♚",
    };

    public static string TurnStatus(Game game, bool computerThinking)
    {
        if (game.IsOver) return Result(game);
        var side = Color(game.Position.SideToMove);
        var check = game.Position.IsInCheck(game.Position.SideToMove) ? " Schach!" : "";
        return computerThinking ? $"{side} (Computer) denkt nach ...{check}" : $"{side} ist am Zug.{check}";
    }

    public static string Result(Game game)
    {
        var winner = game.Winner is { } w ? Color(w) : "";
        var loser = game.ForfeitedBy is { } l ? Color(l) : "";
        return game.Status switch
        {
            GameStatus.Checkmate => $"Schachmatt! {winner} gewinnt.",
            GameStatus.Stalemate => "Patt! Die Partie endet unentschieden.",
            GameStatus.ThreefoldRepetition => "Remis durch dreifache Stellungswiederholung.",
            GameStatus.FiftyMoveRule => "Remis durch die 50-Züge-Regel.",
            GameStatus.InsufficientMaterial => "Remis: Kein Matt mehr möglich.",
            GameStatus.TimeForfeit when game.Winner is null => $"{loser} hat die Zeit überschritten. Remis, der Gegner kann nicht mehr mattsetzen.",
            GameStatus.TimeForfeit => $"{loser} hat die Zeit überschritten. {winner} gewinnt.",
            GameStatus.Resignation => $"{loser} gibt auf. {winner} gewinnt.",
            _ => "",
        };
    }

    public static string Clock(TimeSpan time)
    {
        if (time < TimeSpan.FromSeconds(10)) return $"0:{time.Seconds:00}.{time.Milliseconds / 100}";
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    /// <summary>Move list as "1. e4 e5" lines.</summary>
    public static IEnumerable<string> MoveListLines(Game game)
    {
        var start = Position.FromFen(game.StartFen);
        var number = start.FullmoveNumber;
        var san = game.SanMoves;
        var i = 0;
        if (start.SideToMove == PieceColor.Black && san.Count > 0)
        {
            yield return $"{number}. ...  {san[0]}";
            number++;
            i = 1;
        }
        for (; i < san.Count; i += 2, number++)
        {
            var black = i + 1 < san.Count ? san[i + 1] : "";
            yield return $"{number}. {san[i],-8}{black}";
        }
    }
}
