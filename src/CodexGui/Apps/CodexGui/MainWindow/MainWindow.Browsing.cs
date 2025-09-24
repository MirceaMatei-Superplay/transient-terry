using Avalonia.Controls;
using System.IO;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    async Task BrowseForFolder(TextBox target)
    {
#pragma warning disable CS0618
        var dialog = new OpenFolderDialog();
        var result = await dialog.ShowAsync(this);
#pragma warning restore CS0618
        if (result != null)
            target.Text = result;
    }

    async Task BrowseForUnityPath(TextBox target)
    {
#pragma warning disable CS0618
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters = { new FileDialogFilter { Name = "Executable files", Extensions = { "exe" } } }
        };
#pragma warning restore CS0618

        if (File.Exists(target.Text))
            dialog.Directory = Path.GetDirectoryName(target.Text);
        else if (Directory.Exists(target.Text))
            dialog.Directory = target.Text;

        var result = await dialog.ShowAsync(this);
        if (result?.Length > 0)
        {
            target.Text = result[0];
            return;
        }

        await BrowseForFolder(target);
    }
}
