using Chess.Core;
using Chess.Core.Session;
using FpsChess.App;
using FpsChess.Ui;
using Godot;

namespace FpsChess.Board2D;

/// <summary>2D mode: board on the left, players, clocks, status and move list on the right.</summary>
public partial class Game2DScreen : Control
{
    private Main _main = null!;
    private GameSession _session = null!;

    private BoardView2D _board = null!;
    private PlayerCard _topCard = null!;
    private PlayerCard _bottomCard = null!;
    private Label _status = null!;
    private RichTextLabel _moves = null!;
    private Label _toast = null!;
    private Button _undo = null!;
    private Button _resign = null!;
    private PromotionPicker _promotion = null!;
    private GameOverDialog _gameOver = null!;
    private List<Move> _pendingPromotion = [];
    private double _toastSeconds;

    public void Init(Main main, GameSession session)
    {
        _main = main;
        _session = session;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Get();

        var background = new ColorRect { Color = UiTheme.Background };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(row);

        _board = new BoardView2D
        {
            // Against the computer as black, show black at the bottom.
            Flipped = _session.Setup.White == PlayerKind.Computer && _session.Setup.Black == PlayerKind.Human,
        };
        _board.Init(_session);
        _board.PromotionNeeded += AskPromotion;
        row.AddChild(_board);
        row.AddChild(BuildSidePanel());

        _promotion = new PromotionPicker();
        AddChild(_promotion);
        _promotion.Picked += type =>
        {
            var move = _pendingPromotion.First(m => m.Promotion == type);
            _board.Play(move);
        };
        _promotion.Cancelled += _board.ClearSelection;

        _gameOver = new GameOverDialog();
        AddChild(_gameOver);
        _gameOver.RematchRequested += _main.Rematch;
        _gameOver.MenuRequested += () => _main.ShowMenu();

        _session.MovePlayed += OnMovePlayed;
        _session.PositionReset += RefreshMoves;
        _session.GameEnded += OnGameEnded;

        ArrangeCards();
        RefreshMoves();
        if (_session.Game.IsOver) CallDeferred(MethodName.OnGameEnded);
    }

    public override void _ExitTree()
    {
        _session.MovePlayed -= OnMovePlayed;
        _session.PositionReset -= RefreshMoves;
        _session.GameEnded -= OnGameEnded;
    }

    private Control BuildSidePanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Panel, radius: 0, padding: 22));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);

        _topCard = new PlayerCard();
        _bottomCard = new PlayerCard();
        _status = UiTheme.Label("", 20, bold: true);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(0, 56);

        _moves = new RichTextLabel { ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        _moves.AddThemeFontSizeOverride("normal_font_size", 17);

        var buttons = new GridContainer { Columns = 2 };
        buttons.AddThemeConstantOverride("h_separation", 10);
        buttons.AddThemeConstantOverride("v_separation", 10);
        _undo = UiTheme.Button("Zug zurück", () => _session.Undo());
        _resign = UiTheme.Button("Aufgeben", () => _session.Resign());
        foreach (var button in new[]
                 {
                     _undo,
                     UiTheme.Button("Brett drehen", FlipBoard),
                     UiTheme.Button("Speichern", Save),
                     _resign,
                 })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            buttons.AddChild(button);
        }
        var menu = UiTheme.Button("Hauptmenü", () => _main.ShowMenu());

        _toast = UiTheme.Label("", 15, UiTheme.Accent);
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        column.AddChild(UiTheme.Label("Partie", 26, UiTheme.Accent, bold: true));
        column.AddChild(_topCard);
        column.AddChild(_bottomCard);
        column.AddChild(_status);
        column.AddChild(UiTheme.Label("Züge", 16, UiTheme.Muted));
        column.AddChild(_moves);
        column.AddChild(buttons);
        column.AddChild(menu);
        column.AddChild(_toast);
        panel.AddChild(column);
        return panel;
    }

    public override void _Process(double delta)
    {
        _session.Update(TimeSpan.FromSeconds(delta));

        _status.Text = Texts.TurnStatus(_session.Game, _session.IsComputerThinking);
        _undo.Disabled = !_session.CanUndo;
        _resign.Disabled = _session.Game.IsOver;
        _topCard.Refresh(_session);
        _bottomCard.Refresh(_session);

        if (_toastSeconds > 0)
        {
            _toastSeconds -= delta;
            if (_toastSeconds <= 0) _toast.Text = "";
        }
    }

    private void ArrangeCards()
    {
        var bottom = _board.Flipped ? PieceColor.Black : PieceColor.White;
        _bottomCard.Configure(_session.Setup, bottom);
        _topCard.Configure(_session.Setup, bottom.Opponent());
    }

    private void FlipBoard()
    {
        _board.Flipped = !_board.Flipped;
        _board.QueueRedraw();
        ArrangeCards();
    }

    private void Save()
    {
        _toast.Text = _main.SaveCurrentGame();
        _toastSeconds = 5;
    }

    private void AskPromotion(List<Move> moves)
    {
        _pendingPromotion = moves;
        _promotion.Open(_session.Turn);
    }

    private void OnMovePlayed(PlayedMove _) => RefreshMoves();

    private void RefreshMoves()
    {
        _moves.Text = string.Join("\n", Texts.MoveListLines(_session.Game));
    }

    private void OnGameEnded() => _gameOver.Open(Texts.Result(_session.Game));

    /// <summary>Name, side and clock for one player; highlighted while it is their turn.</summary>
    private sealed partial class PlayerCard : PanelContainer
    {
        private Label _name = null!;
        private Label _clock = null!;
        private PieceColor _color;

        public void Configure(GameSetup setup, PieceColor color)
        {
            _color = color;
            if (_name is null)
            {
                var row = new HBoxContainer();
                _name = UiTheme.Label("", 18);
                _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                _clock = UiTheme.Label("", 28, bold: true);
                row.AddChild(_name);
                row.AddChild(_clock);
                AddChild(row);
            }
            _name.Text = $"{Texts.Glyph(PieceType.King)}  {Texts.Color(color)}: {Texts.Player(setup, color)}";
        }

        public void Refresh(GameSession session)
        {
            var active = !session.Game.IsOver && session.Turn == _color;
            AddThemeStyleboxOverride("panel", UiTheme.Box(active ? UiTheme.PanelLight : UiTheme.Background,
                active ? UiTheme.Accent : UiTheme.PanelLight, 2, padding: 14));

            if (session.Clock is { } clock)
            {
                var remaining = clock.Remaining(_color);
                _clock.Text = Texts.Clock(remaining);
                _clock.AddThemeColorOverride("font_color", remaining < TimeSpan.FromSeconds(20) ? UiTheme.Danger : UiTheme.Text);
            }
            else
            {
                _clock.Text = "";
            }
        }
    }
}
