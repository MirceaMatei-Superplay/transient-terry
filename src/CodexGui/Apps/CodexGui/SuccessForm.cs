using System.Diagnostics;

namespace CodexGui.Apps.CodexGui;

public class SuccessForm : Form
{
    public SuccessForm(string message, string linkText, string url)
    {
        Text = "Success";
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        Controls.Add(layout);

        var label = new Label { Text = message, AutoSize = true };
        layout.Controls.Add(label);

        var link = new LinkLabel { Text = linkText, AutoSize = true };
        link.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        layout.Controls.Add(link);

        var button = new Button { Text = "OK", AutoSize = true, Anchor = AnchorStyles.Top };
        button.Click += (_, _) => Close();
        layout.Controls.Add(button);
    }
}
