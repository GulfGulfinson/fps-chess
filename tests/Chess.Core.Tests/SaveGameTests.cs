using Chess.Core;
using Chess.Core.Persistence;

namespace Chess.Core.Tests;

public class SaveGameTests
{
    private static Game PlayedGame()
    {
        var game = new Game();
        foreach (var uci in new[] { "e2e4", "e7e5", "g1f3", "b8c6" }) game.MakeMove(Move.ParseUci(uci));
        return game;
    }

    [Fact]
    public void RoundTripRestoresMovesSetupAndClock()
    {
        var setup = new GameSetup(PlayerKind.Human, PlayerKind.Computer, AiLevel: 3,
            new TimeControl(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2)));
        var clock = ChessClock.Restore(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(250.5), TimeSpan.FromSeconds(280));
        var original = new SavedGame(setup, PlayedGame(), clock);

        var loaded = GameSerializer.Deserialize(GameSerializer.Serialize(original));

        Assert.Equal(setup, loaded.Setup);
        Assert.Equal(original.Game.Moves, loaded.Game.Moves);
        Assert.Equal(original.Game.Position.ToFen(), loaded.Game.Position.ToFen());
        Assert.NotNull(loaded.Clock);
        Assert.Equal(TimeSpan.FromSeconds(250.5), loaded.Clock.Remaining(PieceColor.White));
        Assert.Equal(TimeSpan.FromSeconds(280), loaded.Clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public void UntimedGameHasNoClock()
    {
        var setup = new GameSetup(PlayerKind.Human, PlayerKind.Human);

        var loaded = GameSerializer.Deserialize(GameSerializer.Serialize(new SavedGame(setup, PlayedGame(), null)));

        Assert.Null(loaded.Clock);
        Assert.Null(loaded.Setup.TimeControl);
    }

    [Fact]
    public void ResignationResultSurvivesRoundTrip()
    {
        var game = PlayedGame();
        game.Resign(PieceColor.White);

        var loaded = GameSerializer.Deserialize(GameSerializer.Serialize(
            new SavedGame(new GameSetup(PlayerKind.Human, PlayerKind.Human), game, null)));

        Assert.Equal(GameStatus.Resignation, loaded.Game.Status);
        Assert.Equal(PieceColor.Black, loaded.Game.Winner);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"version":1,"startFen":"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1","moves":["e2e5"],"white":"Human","black":"Human","aiLevel":2}""")]
    [InlineData("""{"version":1,"startFen":"garbage","moves":[],"white":"Human","black":"Human","aiLevel":2}""")]
    [InlineData("""{"version":99,"startFen":"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1","moves":[],"white":"Human","black":"Human","aiLevel":2}""")]
    public void CorruptOrTamperedFilesAreRejected(string json)
    {
        Assert.Throws<InvalidSaveFileException>(() => GameSerializer.Deserialize(json));
    }

    [Fact]
    public void SavesAndLoadsFromDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"chess-test-{Guid.NewGuid():N}.json");
        try
        {
            GameSerializer.SaveToFile(new SavedGame(new GameSetup(PlayerKind.Human, PlayerKind.Human), PlayedGame(), null), path);

            Assert.Equal(4, GameSerializer.LoadFromFile(path).Game.Moves.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class DuelSaveGameTests
{
    private static readonly GameSetup DuelSetup = new(PlayerKind.Human, PlayerKind.Computer, Variant: GameVariant.Duel);

    [Fact]
    public void DuelOutcomesSurviveRoundTrip()
    {
        var game = new Game(Position.StartFen, GameVariant.Duel);
        foreach (var uci in new[] { "e2e4", "d7d5", "e4d5" }) game.MakeMove(Move.ParseUci(uci));
        game.ResolveDuel(DuelOutcome.DefenderWon);
        game.MakeMove(Move.ParseUci("c8g4"));
        game.MakeMove(Move.ParseUci("d1g4"));
        game.ResolveDuel(DuelOutcome.AttackerWon);

        var json = GameSerializer.Serialize(new SavedGame(DuelSetup, game, null));
        var loaded = GameSerializer.Deserialize(json);

        Assert.Contains("\"e4d5:D\"", json);
        Assert.Contains("\"d1g4:A\"", json);
        Assert.Equal(GameVariant.Duel, loaded.Setup.Variant);
        Assert.Equal(GameVariant.Duel, loaded.Game.Variant);
        Assert.Equal(game.Position.ToFen(), loaded.Game.Position.ToFen());
        Assert.Equal(game.DuelOutcomes, loaded.Game.DuelOutcomes);
    }

    [Fact]
    public void SavingDuringADuelIsRejected()
    {
        var game = new Game(Position.StartFen, GameVariant.Duel);
        foreach (var uci in new[] { "e2e4", "d7d5", "e4d5" }) game.MakeMove(Move.ParseUci(uci));

        Assert.Throws<InvalidOperationException>(() => GameSerializer.Serialize(new SavedGame(DuelSetup, game, null)));
    }

    [Fact]
    public void VersionOneFilesStillLoadAsClassic()
    {
        const string json = """{"version":1,"startFen":"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1","moves":["e2e4"],"white":"Human","black":"Human","aiLevel":2}""";

        var loaded = GameSerializer.Deserialize(json);

        Assert.Equal(GameVariant.Classic, loaded.Setup.Variant);
        Assert.Single(loaded.Game.Moves);
    }

    [Theory]
    [InlineData("e2e4:A")]
    [InlineData("e4d5:X")]
    public void OutcomeMarkersMustMatchCaptures(string move)
    {
        var json = $$"""{"version":2,"variant":"Duel","startFen":"rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 2","moves":["{{move}}"],"white":"Human","black":"Human","aiLevel":2}""";

        Assert.Throws<InvalidSaveFileException>(() => GameSerializer.Deserialize(json));
    }
}
