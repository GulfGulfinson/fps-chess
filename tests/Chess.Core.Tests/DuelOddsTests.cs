using Chess.Core;
using Chess.Core.Ai;
using Chess.Core.Duels;

namespace Chess.Core.Tests;

public class DuelOddsTests
{
    [Fact]
    public void EqualPiecesFavourTheDefenderSlightly()
    {
        var p = DuelOdds.AttackerWinProbability(PieceType.Pawn, PieceType.Pawn);

        Assert.InRange(p, 0.4f, 0.5f);
    }

    [Fact]
    public void StrongerAttackersWinMoreOften()
    {
        Assert.True(DuelOdds.AttackerWinProbability(PieceType.Rook, PieceType.Pawn) >
                    DuelOdds.AttackerWinProbability(PieceType.Pawn, PieceType.Rook));
    }

    [Fact]
    public void NoDuelIsEverCertain()
    {
        foreach (var attacker in Enum.GetValues<PieceType>())
        foreach (var defender in Enum.GetValues<PieceType>())
        {
            Assert.InRange(DuelOdds.AttackerWinProbability(attacker, defender), 0.15f, 0.85f);
        }
    }
}

public class DuelAwareAiTests
{
    // The white rook can take a lone black pawn, which no black piece defends. Depth 1 keeps the comparison
    // honest: deeper, the classic search sees the pawn cannot escape and may postpone the capture.
    private const string FreePawnForARook = "4k3/8/8/3p4/8/8/8/3RK3 w - - 0 1";

    [Fact]
    public void ClassicAiTakesTheFreePawn()
    {
        var move = new ChessAi(1, new Random(1)).ChooseMove(Position.FromFen(FreePawnForARook));

        Assert.Equal("d1d5", move.ToUci());
    }

    [Fact]
    public void DuelAwareAiDoesNotRiskItsRookForAPawn()
    {
        var move = new ChessAi(1, new Random(1), GameVariant.Duel).ChooseMove(Position.FromFen(FreePawnForARook));

        Assert.NotEqual("d1d5", move.ToUci());
    }

    [Fact]
    public void DuelAwareAiStillAttacksAQueenWithAPawn()
    {
        // Winning the queen is worth the risk of losing a pawn.
        var move = new ChessAi(2, new Random(1), GameVariant.Duel).ChooseMove(Position.FromFen("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1"));

        Assert.Equal("e4d5", move.ToUci());
    }
}
