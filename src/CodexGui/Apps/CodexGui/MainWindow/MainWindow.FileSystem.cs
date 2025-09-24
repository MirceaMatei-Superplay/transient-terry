using Common.Scripts;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    async Task OpenFolderForScope(string scope)
    {
        var runtime = Helpers.PrepareRuntime();
        var scopePath = Path.Combine(runtime, scope);
        Directory.CreateDirectory(scopePath);

        if (TryOpenFolder(scopePath))
            return;

        await ShowMessage(string.Format(CultureInfo.InvariantCulture,
            "Failed to open folder at {0}.", scopePath));
    }

    bool TryOpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Win32Exception)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return TryOpenFolderWithShell(path);
    }

    bool TryOpenFolderWithShell(string path)
    {
        var command = GetFolderOpenCommand();
        if (command == null)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = command.Value.fileName,
                Arguments = string.Format(CultureInfo.InvariantCulture,
                    command.Value.argumentFormat,
                    path),
                UseShellExecute = false
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    (string fileName, string argumentFormat)? GetFolderOpenCommand()
    {
        if (OperatingSystem.IsWindows())
            return ("explorer", "\"{0}\"");
        if (OperatingSystem.IsLinux())
            return ("xdg-open", "\"{0}\"");
        if (OperatingSystem.IsMacOS())
            return ("open", "\"{0}\"");
        return null;
    }
}
