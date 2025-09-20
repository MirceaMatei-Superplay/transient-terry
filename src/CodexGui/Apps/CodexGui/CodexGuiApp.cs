using Avalonia;
using Avalonia.ReactiveUI;

namespace CodexGui.Apps.CodexGui;

internal static class CodexGuiApp
{
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}
