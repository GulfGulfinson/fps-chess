using Godot;

namespace FpsChess.App;

public enum GraphicsLevel
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The 3D quality preset. Low and medium render the 3D scene at reduced resolution and upscale it with
/// FSR, which is what makes the game playable on integrated graphics; the 2D interface stays sharp.
/// </summary>
public static class GraphicsQuality
{
    public static GraphicsLevel Current => UserPrefs.Graphics;

    public static float RenderScale => Current switch
    {
        GraphicsLevel.Low => 0.6f,
        GraphicsLevel.Medium => 0.8f,
        _ => 1f,
    };

    public static bool Shadows => Current != GraphicsLevel.Low;
    public static bool Glow => Current != GraphicsLevel.Low;

    public static DirectionalLight3D.ShadowMode ShadowSplits =>
        Current == GraphicsLevel.High ? DirectionalLight3D.ShadowMode.Parallel4Splits : DirectionalLight3D.ShadowMode.Parallel2Splits;

    /// <summary>Applies the 3D render scale to a viewport (the main window for FPS Chess).</summary>
    public static void ApplyTo(Viewport viewport)
    {
        viewport.Scaling3DMode = RenderScale < 1f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = RenderScale;
    }

    public static void ResetViewport(Viewport viewport)
    {
        viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = 1f;
    }

    public static string Label(GraphicsLevel level) => level switch
    {
        GraphicsLevel.Low => "Niedrig (schnell, für Laptops)",
        GraphicsLevel.Medium => "Mittel",
        _ => "Hoch",
    };
}
