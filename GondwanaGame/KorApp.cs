using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace KorFrontier.Gondwana;

internal sealed class KorApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new KorWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
