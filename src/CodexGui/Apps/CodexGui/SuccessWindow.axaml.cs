using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CodexGui.Apps.CodexGui;

public partial class SuccessWindow : Window
{
    readonly string _url;

    public SuccessWindow()
    {
        InitializeComponent();
        _url = string.Empty;
    }

    public SuccessWindow(string message, string linkText, string url)
    {
        InitializeComponent();
        var messageTextBlock = this.FindControl<TextBlock>("messageText");
        if (messageTextBlock == null)
            throw new InvalidOperationException("Message text block not found");

        var linkTextBlock = this.FindControl<TextBlock>("link");
        if (linkTextBlock == null)
            throw new InvalidOperationException("Link text block not found");

        messageTextBlock.Text = message;
        linkTextBlock.Text = linkText;
        _url = url ?? string.Empty;

        linkTextBlock.PointerPressed += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_url))
                return;

            Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true });
        };
    }

    void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    void OnClose(object? sender, RoutedEventArgs e)
        => Close();
}
