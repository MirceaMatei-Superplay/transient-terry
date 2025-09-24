using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CodexGui.Apps.CodexGui;

public partial class CallstackOverlay : UserControl
{
    readonly TextBlock _headerText;
    readonly TextBlock _callstackText;

    TaskCompletionSource<object?>? _completionSource;

    public CallstackOverlay()
    {
        InitializeComponent();

        var headerText = this.FindControl<TextBlock>("headerText");
        if (headerText == null)
            throw new InvalidOperationException("Header text control was not found.");

        var callstackText = this.FindControl<TextBlock>("callstackText");
        if (callstackText == null)
            throw new InvalidOperationException("Callstack text control was not found.");

        _headerText = headerText;
        _callstackText = callstackText;
    }

    public Task ShowAsync(string title, string callstack)
    {
        if (_completionSource != null)
            throw new InvalidOperationException("Overlay is already visible.");

        _headerText.Text = title;
        _callstackText.Text = callstack;

        IsVisible = true;
        IsHitTestVisible = true;
        Focus();

        _completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _completionSource.Task;
    }

    void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    void OnClose(object? sender, RoutedEventArgs e)
        => Complete();

    async void OnCopyCallstack(object? sender, RoutedEventArgs e)
    {
        var text = _callstackText.Text;
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

        _completionSource.TrySetResult(null);
        _completionSource = null;
    }
}
