using Chess.Core;
using Chess.Core.Duels;

namespace Chess.Core.Tests;

public class TrainingDuelTests
{
    public static TheoryData<PieceType, PieceType> AllMatchups()
    {
        var data = new TheoryData<PieceType, PieceType>();
        foreach (var attacker in Enum.GetValues<PieceType>())
        foreach (var defender in Enum.GetValues<PieceType>())
            data.Add(attacker, defender);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllMatchups))]
    public void EveryMatchupStartsWithALegalCaptureIntoADuel(PieceType attacker, PieceType defender)
    {
        var (fen, capture) = TrainingDuel.Create(attacker, defender);
        var game = new Game(fen, GameVariant.Duel);

        Assert.True(game.TryMakeMove(capture), $"{capture} is not legal in {fen}");
        Assert.Equal(new Piece(attacker, PieceColor.White), game.PendingDuel?.Attacker);
        Assert.Equal(new Piece(defender, PieceColor.Black), game.PendingDuel?.Defender);
    }
}
