using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace CodexGui.Apps.CodexGui;

public partial class App : Application
{
    public override void Initialize()
        => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CodexGui/Assets/app_icon.png")))
            };

        base.OnFrameworkInitializationCompleted();
    }
}
