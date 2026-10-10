using Chess.Core;
using Chess.Core.Stats;

namespace Chess.Core.Tests;

public class CareerStatsTests
{
    [Fact]
    public void CountsDuelsPerPiece()
    {
        var stats = new CareerStats();

        stats.RecordDuel(PieceType.Bishop, won: true, headshots: 2, damageDealt: 160);
        stats.RecordDuel(PieceType.Bishop, won: false, headshots: 0, damageDealt: 40);
        stats.RecordDuel(PieceType.Pawn, won: true, headshots: 1, damageDealt: 110);

        Assert.Equal(3, stats.Duels);
        Assert.Equal(2, stats.DuelsWon);
        Assert.Equal(3, stats.Headshots);
        Assert.Equal(310, stats.DamageDealt);
        Assert.Equal(2f / 3f, stats.DuelWinRate, 3);
        Assert.Equal(PieceType.Bishop, stats.FavouritePiece);
    }

    [Fact]
    public void BestPieceNeedsEnoughDuels()
    {
        var stats = new CareerStats();
        stats.RecordDuel(PieceType.Queen, won: true, headshots: 0, damageDealt: 0);
        for (var i = 0; i < 3; i++) stats.RecordDuel(PieceType.Rook, won: i < 2, headshots: 0, damageDealt: 0);

        // One won queen duel is luck; the rook's 2 of 3 is a record.
        Assert.Equal(PieceType.Rook, stats.BestPiece);
    }

    [Fact]
    public void CountsGameResults()
    {
        var stats = new CareerStats();

        stats.RecordGame(GameResultKind.Won);
        stats.RecordGame(GameResultKind.Lost);
        stats.RecordGame(GameResultKind.Draw);
        stats.RecordGame(GameResultKind.Won);

        Assert.Equal(4, stats.Games);
        Assert.Equal(2, stats.GamesWon);
        Assert.Equal(1, stats.GamesDrawn);
        Assert.Equal(1, stats.GamesLost);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var stats = new CareerStats();
        stats.RecordDuel(PieceType.Knight, won: true, headshots: 1, damageDealt: 99);
        stats.RecordGame(GameResultKind.Won);

        var loaded = CareerStats.FromJson(stats.ToJson());

        Assert.Equal(1, loaded.Duels);
        Assert.Equal(PieceType.Knight, loaded.FavouritePiece);
        Assert.Equal(1, loaded.GamesWon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"duels\": \"many\"}")]
    public void BrokenDataStartsFresh(string json)
    {
        Assert.Equal(0, CareerStats.FromJson(json).Duels);
    }

    [Fact]
    public void EmptyStatsHaveNoFavourite()
    {
        var stats = new CareerStats();

        Assert.Null(stats.FavouritePiece);
        Assert.Null(stats.BestPiece);
        Assert.Equal(0f, stats.DuelWinRate);
    }
}
