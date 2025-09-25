using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CodexGui.Apps.CodexGui;

public partial class MessageOverlay : UserControl
{
    readonly TextBlock _headerText;
    readonly TextBlock _iconText;
    readonly Border _iconContainer;
    readonly SelectableTextBlock _messageText;
    readonly Button _viewCallstackButton;

    TaskCompletionSource<object?>? _completionSource;
    string? _callstack;
    string _currentTitle = "Message";

    public MessageOverlay()
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

        var messageText = this.FindControl<SelectableTextBlock>("messageText");
        if (messageText == null)
            throw new InvalidOperationException("Message text control was not found.");

        var viewCallstackButton = this.FindControl<Button>("viewCallstackButton");
        if (viewCallstackButton == null)
            throw new InvalidOperationException("Callstack button was not found.");

        _headerText = headerText;
        _iconText = iconText;
        _iconContainer = iconContainer;
        _messageText = messageText;
        _viewCallstackButton = viewCallstackButton;
    }

    public Task ShowAsync(string message,
        string title = "Message",
        string iconGlyph = "ℹ",
        string accentColor = "#42D77D",
        string accentBackground = "#214329",
        string? callstack = null)
    {
        if (_completionSource != null)
            throw new InvalidOperationException("Overlay is already visible.");

        _currentTitle = title;
        _headerText.Text = title;
        _iconText.Text = iconGlyph;
        _iconText.Foreground = Brush.Parse(accentColor);
        _iconContainer.Background = Brush.Parse(accentBackground);
        _messageText.Text = message;
        _callstack = callstack;
        _viewCallstackButton.IsVisible = string.IsNullOrWhiteSpace(callstack) == false;

        IsVisible = true;
        IsHitTestVisible = true;
        Focus();

        _completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _completionSource.Task;
    }

    public event EventHandler<CallstackRequestedEventArgs>? CallstackRequested;

    void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    void OnClose(object? sender, RoutedEventArgs e)
        => Complete();

    void OnViewCallstack(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_callstack))
            return;

        CallstackRequested?.Invoke(this, new CallstackRequestedEventArgs(_currentTitle, _callstack));
    }

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

    void Complete()
    {
        if (_completionSource == null)
            return;

        IsVisible = false;
        IsHitTestVisible = false;
        _viewCallstackButton.IsVisible = false;
        _callstack = null;

        _completionSource.TrySetResult(null);
        _completionSource = null;
    }

    public sealed class CallstackRequestedEventArgs : EventArgs
    {
        public CallstackRequestedEventArgs(string title, string callstack)
        {
            Title = title;
            Callstack = callstack;
        }

        public string Title { get; }

        public string Callstack { get; }
    }
}
