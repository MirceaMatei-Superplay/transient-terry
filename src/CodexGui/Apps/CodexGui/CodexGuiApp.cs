namespace CodexGui.Apps.CodexGui;

internal static class CodexGuiApp
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
