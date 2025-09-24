using Avalonia.Controls;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    async Task ShowMessage(string message,
        string title = "Message",
        string iconGlyph = "ℹ",
        string accentColor = "#42D77D",
        string accentBackground = "#214329",
        string? callstack = null)
    {
        await _messageOverlay.ShowAsync(message,
            title,
            iconGlyph,
            accentColor,
            accentBackground,
            callstack);
    }

    Task ShowCallstack(string title, string callstack)
        => _callstackOverlay.ShowAsync(title, callstack);

    async void OnMessageCallstackRequested(object? sender, MessageOverlay.CallstackRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Callstack))
            return;

        var title = string.Format(CultureInfo.InvariantCulture,
            "{0} Callstack",
            e.Title);
        await ShowCallstack(title, e.Callstack);
    }

    Task ShowWindowWithoutActivation(Window dialog)
    {
        dialog.ShowActivated = false;
        dialog.ShowInTaskbar = false;

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleClosed(object? sender, EventArgs _)
        {
            dialog.Closed -= HandleClosed;
            completionSource.TrySetResult(null);
        }

        dialog.Closed += HandleClosed;

        dialog.Show(this);

        return completionSource.Task;
    }

    async Task CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }
}
