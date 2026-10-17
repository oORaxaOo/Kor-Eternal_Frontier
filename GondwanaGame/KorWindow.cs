using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Gondwana.Avalonia.Rendering;
using KorFrontier.Gondwana.Simulation;

namespace KorFrontier.Gondwana;

internal sealed class KorWindow : Window
{
    private static readonly Color Night = Color.Parse("#11151B");
    private static readonly Color Panel = Color.Parse("#1A2028");
    private static readonly Color Crimson = Color.Parse("#C42D43");
    private static readonly Color Gold = Color.Parse("#D9B56D");
    private readonly AvaloniaGpuRenderSurfaceControl _surface = new();
    private readonly TextBlock _resources = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _blight = new();
    private KorGameHost? _host;
    private readonly DispatcherTimer _hudTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    internal KorWindow()
    {
        Title = "KOR // ETERNAL FRONTIER";
        Width = 1440;
        Height = 900;
        MinWidth = 1040;
        MinHeight = 680;
        Background = new SolidColorBrush(Night);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Content = root;

        var header = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A252D")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(22, 14)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var brand = new StackPanel { Spacing = 2 };
        brand.Children.Add(new TextBlock { Text = "K O R  /  E T E R N A L   F R O N T I E R", FontSize = 19, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Gold) });
        brand.Children.Add(new TextBlock { Text = "ALLE FÜR ALLE  ·  EIN KOR LÄSST KEINEN KOR ALLEIN", FontSize = 10, LetterSpacing = 1.8, Foreground = new SolidColorBrush(Color.Parse("#AEB6C1")) });
        headerGrid.Children.Add(brand);
        var stamp = new TextBlock { Text = "FRONTIER COMMAND  /  001", FontSize = 11, Foreground = new SolidColorBrush(Crimson), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(stamp, 1);
        headerGrid.Children.Add(stamp);
        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        main.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(286)));
        Grid.SetRow(main, 1);
        root.Children.Add(main);

        _surface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _surface.VerticalAlignment = VerticalAlignment.Stretch;
        Grid.SetColumn(_surface, 0);
        main.Children.Add(_surface);

        var side = new Border
        {
            Background = new SolidColorBrush(Panel),
            BorderBrush = new SolidColorBrush(Color.Parse("#323943")),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(16)
        };
        var commands = new StackPanel { Spacing = 10 };
        commands.Children.Add(SectionLabel("SETTLEMENT STATUS"));
        _resources.FontSize = 13;
        _resources.Foreground = new SolidColorBrush(Color.Parse("#E6E9ED"));
        _resources.TextWrapping = TextWrapping.Wrap;
        commands.Children.Add(_resources);
        _blight.FontSize = 12;
        _blight.Foreground = new SolidColorBrush(Color.Parse("#E56A7A"));
        commands.Children.Add(_blight);
        commands.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#39404A")), Margin = new Thickness(0, 4) });
        commands.Children.Add(SectionLabel("CONSTRUCTION"));
        commands.Children.Add(CommandButton("01  /  SOLAR ARRAY", "Energy infrastructure · 90 supplies / 20 alloy", () => _host?.SetBuildMode(KorBuildKind.Solar)));
        commands.Children.Add(CommandButton("02  /  ORE EXTRACTOR", "Alloy production · 110 supplies / 45 alloy", () => _host?.SetBuildMode(KorBuildKind.Extractor)));
        commands.Children.Add(CommandButton("03  /  KNOWLEDGE ARCHIVE", "Research economy · 120 supplies / 25 alloy / 10 energy", () => _host?.SetBuildMode(KorBuildKind.Archive)));
        commands.Children.Add(CommandButton("04  /  RELAY BEACON", "Supply network · 80 supplies / 15 alloy", () => _host?.SetBuildMode(KorBuildKind.Relay)));
        commands.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#39404A")), Margin = new Thickness(0, 4) });
        commands.Children.Add(SectionLabel("COMMANDS"));
        commands.Children.Add(CommandButton("SELECT / MOVE", "Left-click a Kor · right-click ground to move", () => _host?.CancelBuildMode()));
        commands.Children.Add(CommandButton("CENTER ON CORE", "Return to the heart of the settlement", () => _host?.CenterOnCore()));
        commands.Children.Add(CommandButton("CANCEL ORDER", "Escape cancels the active build mode", () => _host?.CancelBuildMode()));
        commands.Children.Add(new TextBlock
        {
            Text = "WASD / ARROWS  ·  CAMERA\nMOUSE WHEEL  ·  ZOOM\nLEFT CLICK  ·  SELECT\nRIGHT CLICK  ·  ORDER / PLACE\nSHIFT + CLICK  ·  ADD TO SELECTION",
            FontSize = 10,
            LineHeight = 16,
            Foreground = new SolidColorBrush(Color.Parse("#A3ABB5")),
            Margin = new Thickness(0, 8, 0, 0)
        });
        var scroll = new ScrollViewer { Content = commands, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        side.Child = scroll;
        Grid.SetColumn(side, 1);
        main.Children.Add(side);

        var footer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0C0F14")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3A252D")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 10)
        };
        _status.FontSize = 12;
        _status.Foreground = new SolidColorBrush(Color.Parse("#D7DCE2"));
        _status.Text = "INITIALIZING FRONTIER SYSTEMS…";
        footer.Child = _status;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Opened += OnOpened;
        Closed += (_, _) => { _hudTimer.Stop(); _host?.Dispose(); _host = null; };
        _hudTimer.Tick += (_, _) => RefreshHud();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            _host = new KorGameHost(_surface);
            _host.Initialize(logLevel: Microsoft.Extensions.Logging.LogLevel.Warning);
            RefreshHud();
            _hudTimer.Start();
        }
        catch (Exception ex)
        {
            await MessageBoxAsync($"The Kor frontier could not start.\n\n{ex.Message}");
        }
    }

    private void RefreshHud()
    {
        if (_host is null) return;
        var world = _host.World;
        _resources.Text = $"SUPPLIES     {world.Supplies,5}\nALLOY           {world.Alloy,5}\nENERGY        {world.Energy,5}\nKNOWLEDGE  {world.Knowledge,5}\n\nUNITS             {world.Units.Count,5}\nSTRUCTURES  {world.Buildings.Count,5}";
        _blight.Text = $"BLIGHT FOOTPRINT   {world.BlightTiles} TILES";
        _status.Text = _host.BuildMode is { } mode
            ? $"BUILD MODE: {mode.ToString().ToUpperInvariant()}  ·  Right-click an open tile to place.  ·  Esc to cancel."
            : world.Status;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.Bold,
        Foreground = new SolidColorBrush(Gold),
        Margin = new Thickness(0, 4, 0, 2)
    };

    private static Button CommandButton(string title, string subtitle, Action action)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#F1F3F5")) });
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 9, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#AAB2BC")) });
        var button = new Button
        {
            Content = stack,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.Parse("#252D37")),
            BorderBrush = new SolidColorBrush(Color.Parse("#414B58")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 9),
            CornerRadius = new CornerRadius(4)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private async Task MessageBoxAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Kor Frontier startup error",
            Width = 560,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }
                }
            }
        };
        if (dialog.Content is StackPanel stack && stack.Children[1] is Button close)
            close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
