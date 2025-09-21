using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CodexGui.Apps.CodexGui;

public partial class MessageWindow : Window
{
    private readonly TextBlock _headerText;
    private readonly TextBlock _iconText;
    private readonly Border _iconContainer;
    private readonly TextBlock _messageText;

    public MessageWindow()
    {
        InitializeComponent();

        var headerText = this.FindControl<TextBlock>("headerText");
        if (headerText == null)
            throw new InvalidOperationException("Header text control was not found.");

        var iconText = this.FindControl<TextBlock>("iconText");
        if (iconText == null)
            throw new InvalidOperationException("Icon text control was not found.");

        var iconContainer = this.FindControl<Border>("iconContainer");
        if (iconContainer == null)
            throw new InvalidOperationException("Icon container control was not found.");

        var messageText = this.FindControl<TextBlock>("messageText");
        if (messageText == null)
            throw new InvalidOperationException("Message text control was not found.");

        _headerText = headerText;
        _iconText = iconText;
        _iconContainer = iconContainer;
        _messageText = messageText;
    }

    public MessageWindow(string message,
        string title = "Message",
        string iconGlyph = "ℹ",
        string accentColor = "#42D77D",
        string accentBackground = "#214329")
        : this()
    {
        Title = title;
        _headerText.Text = title;
        _iconText.Text = iconGlyph;
        _iconText.Foreground = Brush.Parse(accentColor);
        _iconContainer.Background = Brush.Parse(accentBackground);
        _messageText.Text = message;
    }

    void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    void OnClose(object? sender, RoutedEventArgs e)
        => Close();

    async void OnCopyMessage(object? sender, RoutedEventArgs e)
    {
        var text = _messageText.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }
}
