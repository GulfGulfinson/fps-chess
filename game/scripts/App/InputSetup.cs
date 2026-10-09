using Godot;

namespace FpsChess.App;

/// <summary>Registers the keyboard actions in code so project.godot stays readable.</summary>
public static class InputSetup
{
    public const string Forward = "move_forward";
    public const string Back = "move_back";
    public const string Left = "move_left";
    public const string Right = "move_right";
    public const string Jump = "jump";
    public const string Sprint = "sprint";
    public const string Pause = "pause";

    public static void Register()
    {
        Add(Forward, Key.W, Key.Up);
        Add(Back, Key.S, Key.Down);
        Add(Left, Key.A, Key.Left);
        Add(Right, Key.D, Key.Right);
        Add(Jump, Key.Space);
        Add(Sprint, Key.Shift);
        Add(Pause, Key.Escape);
    }

    private static void Add(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        foreach (var key in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
