using Chess.Core.Duels;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Duel;

/// <summary>Heads-up display during a duel.</summary>
public partial class DuelHud : Control
{
    private Crosshair _crosshair = null!;
    private Bar _ownHealth = null!;
    private Bar _enemyHealth = null!;
    private Label _ownName = null!;
    private Label _enemyName = null!;
    private Label _ammo = null!;
    private Label _perks = null!;
    private Label _timer = null!;
    private Label _center = null!;
    private ColorRect _damageFlash = null!;
    private Tween? _flashTween;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        _damageFlash = new ColorRect { Color = new Color(0.8f, 0f, 0f, 0f), MouseFilter = MouseFilterEnum.Ignore };
        _damageFlash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_damageFlash);

        _crosshair = new Crosshair();
        AddChild(_crosshair);

        // Opponent at the top centre.
        var top = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(420, 0) };
        _enemyName = Outlined(UiTheme.Label("", 18, bold: true));
        _enemyName.HorizontalAlignment = HorizontalAlignment.Center;
        _enemyHealth = new Bar(new Color("e04a3f")) { CustomMinimumSize = new Vector2(420, 18) };
        _timer = Outlined(UiTheme.Label("", 30, bold: true));
        _timer.HorizontalAlignment = HorizontalAlignment.Center;
        top.AddChild(_enemyName);
        top.AddChild(_enemyHealth);
        top.AddChild(_timer);
        AddChild(top);
        top.GrowHorizontal = GrowDirection.Both;
        top.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.KeepSize, 20);

        // Own status bottom left.
        var bottomLeft = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(360, 0) };
        _ownName = Outlined(UiTheme.Label("", 18, bold: true));
        _ownHealth = new Bar(new Color("4fd17a")) { CustomMinimumSize = new Vector2(360, 22) };
        _perks = Outlined(UiTheme.Label("", 16, UiTheme.Muted));
        bottomLeft.AddChild(_ownName);
        bottomLeft.AddChild(_ownHealth);
        bottomLeft.AddChild(_perks);
        AddChild(bottomLeft);
        bottomLeft.GrowVertical = GrowDirection.Begin;
        bottomLeft.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize, 28);

        _ammo = Outlined(UiTheme.Label("", 34, bold: true));
        _ammo.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_ammo);
        _ammo.GrowHorizontal = GrowDirection.Begin;
        _ammo.GrowVertical = GrowDirection.Begin;
        _ammo.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.KeepSize, 28);

        _center = Outlined(UiTheme.Label("", 72, UiTheme.Accent, bold: true));
        _center.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_center);
        _center.GrowHorizontal = GrowDirection.Both;
        _center.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
        _center.Position += new Vector2(0, -140);
    }

    private static Label Outlined(Label label)
    {
        label.AddThemeConstantOverride("outline_size", 8);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        return label;
    }

    public void Setup(FighterClass own, FighterClass enemy, string enemyOwner)
    {
        _ownName.Text = $"{own.Name} ({Texts.Piece(own.Piece)}): {own.Weapon.Name}";
        _enemyName.Text = $"{enemyOwner}: {enemy.Name} ({Texts.Piece(enemy.Piece)}), {enemy.Weapon.Name}";
        _ownHealth.Max = own.MaxHealth;
        _enemyHealth.Max = enemy.MaxHealth;
        _center.Text = "";
    }

    public void Refresh(Fighter own, float ownHealth, float enemyHealth, float timeLeft, float countdown, float intro, bool fighting)
    {
        _ownHealth.Value = ownHealth;
        _enemyHealth.Value = enemyHealth;
        _timer.Text = fighting ? $"{Mathf.CeilToInt(timeLeft)}" : "";
        _timer.AddThemeColorOverride("font_color", timeLeft < 10f ? UiTheme.Danger : UiTheme.Text);

        var weapon = own.Class.Weapon;
        _ammo.Text = own.IsReloading ? "Nachladen ..." : $"{own.Ammo} / {weapon.Magazine}";

        var perks = new List<string>();
        if (own.Class.DoubleJump) perks.Add("Doppelsprung (Leertaste x2)");
        if (own.Class.ZoomFov is not null) perks.Add("Zielfernrohr (Rechtsklick)");
        if (own.Class.Dash is not null) perks.Add(own.DashCooldown > 0 ? $"Dash in {own.DashCooldown:0.0} s" : "Dash bereit (Shift)");
        if (own.Class.Grenades is not null) perks.Add($"Granaten: {own.GrenadesLeft} (G)");
        if (own.Class.Regeneration is not null) perks.Add("Regeneration");
        if (own.Class.DamageTakenMultiplier < 1f) perks.Add("Panzerung");
        if (weapon.SpinUpSeconds > 0) perks.Add($"Drehzahl {own.SpinUp / weapon.SpinUpSeconds:P0}");
        _perks.Text = string.Join("   ", perks);

        if (intro > 0f) _center.Text = "DUELL!";
        else if (countdown > 0f) _center.Text = Mathf.CeilToInt(countdown).ToString();
        else if (_center.Text is "DUELL!" or "1" or "2" or "3") _center.Text = "";
    }

    public void ShowResult(bool won)
    {
        _center.Text = won ? "SIEG!" : "NIEDERLAGE";
        _center.AddThemeColorOverride("font_color", won ? new Color("6bff8f") : UiTheme.Danger);
    }

    public void SetZoomed(bool zoomed) => _crosshair.Zoomed = zoomed;

    public void HitMarker() => _crosshair.Hit();

    public void DamageFlash()
    {
        _flashTween?.Kill();
        _damageFlash.Color = new Color(0.8f, 0f, 0f, 0.35f);
        _flashTween = CreateTween();
        _flashTween.TweenProperty(_damageFlash, "color:a", 0f, 0.4f);
    }

    private sealed partial class Bar : Control
    {
        private readonly Color _color;
        private float _value;

        public Bar(Color color) => _color = color;

        public float Max { get; set; } = 100f;

        public float Value
        {
            get => _value;
            set
            {
                _value = value;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.6f));
            var fraction = Max > 0 ? Mathf.Clamp(_value / Max, 0f, 1f) : 0f;
            DrawRect(new Rect2(new Vector2(2, 2), new Vector2((Size.X - 4) * fraction, Size.Y - 4)), _color);
            var text = $"{Mathf.CeilToInt(_value)} / {Mathf.CeilToInt(Max)}";
            DrawString(UiTheme.Bold, new Vector2(8, Size.Y - 4), text, HorizontalAlignment.Left, -1, (int)(Size.Y * 0.7f), Colors.White);
        }
    }

    private sealed partial class Crosshair : Control
    {
        private float _hitFor;

        public bool Zoomed { get; set; }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public void Hit()
        {
            _hitFor = 0.2f;
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (_hitFor <= 0f) return;
            _hitFor -= (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var center = Size / 2f;
            if (Zoomed)
            {
                // Scope: dark vignette with a thin reticle.
                var radius = Mathf.Min(Size.X, Size.Y) * 0.42f;
                DrawArc(center, radius + 400f, 0, Mathf.Tau, 96, Colors.Black, 800f);
                DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), Colors.Black, 1.5f);
                DrawLine(center - new Vector2(0, radius), center + new Vector2(0, radius), Colors.Black, 1.5f);
            }
            else
            {
                foreach (var direction in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                {
                    DrawLine(center + direction * 6f, center + direction * 15f, Colors.Black, 4f);
                    DrawLine(center + direction * 6f, center + direction * 15f, Colors.White, 2f);
                }
                DrawCircle(center, 1.5f, Colors.White);
            }

            if (_hitFor > 0f)
            {
                foreach (var direction in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                {
                    DrawLine(center + direction * 8f, center + direction * 16f, new Color("ff4a3f"), 3f);
                }
            }
        }
    }
}
