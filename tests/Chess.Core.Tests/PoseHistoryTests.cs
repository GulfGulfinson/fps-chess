using System.Numerics;
using Chess.Core.Duels;

namespace Chess.Core.Tests;

public class PoseHistoryTests
{
    private const float Radius = 0.9f;
    private const float Height = 1.5f;

    [Fact]
    public void AcceptsAHitWhereTheTargetStoodRecently()
    {
        var history = new PoseHistory(0.6f);
        history.Record(0.0f, new Vector3(0, 0, 0));
        history.Record(0.1f, new Vector3(1, 0, 0));
        history.Record(0.2f, new Vector3(2, 0, 0));

        // The shooter saw the target where it was 0.15 s ago.
        Assert.True(history.Contains(new Vector3(1.2f, 1.0f, 0.3f), now: 0.25f, Radius, Height));
    }

    [Fact]
    public void RejectsAHitFarFromEveryRecordedPosition()
    {
        var history = new PoseHistory(0.6f);
        history.Record(0.0f, new Vector3(0, 0, 0));
        history.Record(0.1f, new Vector3(0.5f, 0, 0));

        Assert.False(history.Contains(new Vector3(4, 1, 0), now: 0.15f, Radius, Height));
    }

    [Fact]
    public void RejectsHitsAboveOrBelowTheBody()
    {
        var history = new PoseHistory(0.6f);
        history.Record(0.0f, new Vector3(0, 0, 0));

        Assert.False(history.Contains(new Vector3(0, 2.5f, 0), now: 0.1f, Radius, Height));
        Assert.False(history.Contains(new Vector3(0, -0.8f, 0), now: 0.1f, Radius, Height));
        Assert.True(history.Contains(new Vector3(0, 1.4f, 0), now: 0.1f, Radius, Height));
    }

    [Fact]
    public void ForgetsPositionsOlderThanTheWindow()
    {
        var history = new PoseHistory(0.6f);
        history.Record(0.0f, new Vector3(0, 0, 0));
        history.Record(1.0f, new Vector3(5, 0, 0));

        Assert.False(history.Contains(new Vector3(0, 1, 0), now: 1.0f, Radius, Height));
        Assert.True(history.Contains(new Vector3(5, 1, 0), now: 1.0f, Radius, Height));
    }

    [Fact]
    public void ChecksTheSegmentBetweenSamples()
    {
        // A fast target passes through points between two recorded samples.
        var history = new PoseHistory(0.6f);
        history.Record(0.0f, new Vector3(0, 0, 0));
        history.Record(0.1f, new Vector3(4, 0, 0));

        Assert.True(history.Contains(new Vector3(2, 1, 0), now: 0.1f, Radius, Height));
    }

    [Fact]
    public void EmptyHistoryContainsNothing()
    {
        Assert.False(new PoseHistory(0.6f).Contains(Vector3.Zero, now: 0f, Radius, Height));
    }
}
