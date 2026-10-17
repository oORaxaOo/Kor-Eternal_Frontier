using Avalonia;

namespace KorFrontier.Gondwana;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<KorApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
