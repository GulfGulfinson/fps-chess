using System.Numerics;

namespace Chess.Core.Duels;

/// <summary>
/// Where a fighter stood during the last fraction of a second. The server uses it to accept hits the
/// shooter saw on screen ("favour the shooter"): the target's picture on a client always lags behind the
/// server's position, so checking only the current position would turn clean hits into misses.
/// </summary>
public sealed class PoseHistory(float windowSeconds)
{
    // Margin above the head and below the feet, so shots grazing the model still count.
    private const float VerticalMargin = 0.25f;

    private readonly List<(float Time, Vector3 Feet)> _samples = [];

    public void Record(float time, Vector3 feet)
    {
        _samples.Add((time, feet));
        // Keep one sample older than the window so the segment crossing its start can still be checked.
        while (_samples.Count > 2 && _samples[1].Time < time - windowSeconds) _samples.RemoveAt(0);
    }

    /// <summary>
    /// True if <paramref name="point"/> lay inside the fighter's body (a cylinder of <paramref name="radius"/>
    /// and <paramref name="height"/>) at some moment within the window before <paramref name="now"/>.
    /// Movement between two samples is treated as a straight line.
    /// </summary>
    public bool Contains(Vector3 point, float now, float radius, float height) =>
        HitHeight(point, now, radius, height) is not null;

    /// <summary>
    /// Where on the body the point lies, from 0 (feet) to 1 (top of the head), or null when it missed.
    /// While the target moved vertically (a jump) the most central reading is used, so only hits that are
    /// clearly at the top count as headshots.
    /// </summary>
    public float? HitHeight(Vector3 point, float now, float radius, float height)
    {
        var from = now - windowSeconds;
        float? best = null;
        for (var i = 0; i < _samples.Count; i++)
        {
            var (time, feet) = _samples[i];
            if (time < from) continue;
            var (previousTime, start) = i > 0 ? _samples[i - 1] : _samples[i];
            // Only the part of the movement inside the window counts.
            if (previousTime < from && time > previousTime)
                start = Vector3.Lerp(start, feet, (from - previousTime) / (time - previousTime));
            if (!Inside(point, start, feet, radius, height)) continue;

            var feetY = Math.Clamp(point.Y - height / 2f, MathF.Min(start.Y, feet.Y), MathF.Max(start.Y, feet.Y));
            var relative = (point.Y - feetY) / height;
            if (best is null || MathF.Abs(relative - 0.5f) < MathF.Abs(best.Value - 0.5f)) best = relative;
        }
        return best;
    }

    private static bool Inside(Vector3 point, Vector3 a, Vector3 b, float radius, float height)
    {
        var low = MathF.Min(a.Y, b.Y) - VerticalMargin;
        var high = MathF.Max(a.Y, b.Y) + height + VerticalMargin;
        if (point.Y < low || point.Y > high) return false;

        // Horizontal distance from the point to the segment a..b.
        var p = new Vector2(point.X, point.Z);
        var start = new Vector2(a.X, a.Z);
        var segment = new Vector2(b.X, b.Z) - start;
        var lengthSquared = segment.LengthSquared();
        var t = lengthSquared < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(p - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(p, start + segment * t) <= radius;
    }
}
