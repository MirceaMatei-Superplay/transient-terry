using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CodexGui.Apps.CodexGui;

public partial class CallstackOverlay : UserControl
{
    static readonly IBrush HyperlinkBrush = Brush.Parse("#42D77D");
    static readonly IBrush DefaultLineBrush = Brush.Parse("#D9D9D9");
    static readonly FontFamily CallstackFont = new("Consolas");
    static readonly Thickness LineMargin = new(0, 0, 0, 4);

    readonly TextBlock _headerText;
    readonly StackPanel _callstackPanel;

    string _currentCallstack = string.Empty;

    TaskCompletionSource<object?>? _completionSource;

    public CallstackOverlay()
    {
        InitializeComponent();

        var headerText = this.FindControl<TextBlock>("headerText");
        if (headerText == null)
            throw new InvalidOperationException("Header text control was not found.");

        var callstackPanel = this.FindControl<StackPanel>("callstackPanel");
        if (callstackPanel == null)
            throw new InvalidOperationException("Callstack panel was not found.");

        _headerText = headerText;
        _callstackPanel = callstackPanel;
    }

    public Task ShowAsync(string title, string callstack)
    {
        if (_completionSource != null)
            throw new InvalidOperationException("Overlay is already visible.");

        _headerText.Text = title;
        _currentCallstack = callstack;

        BuildCallstackEntries(callstack);

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
        if (string.IsNullOrWhiteSpace(_currentCallstack))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(_currentCallstack);
    }

    void Complete()
    {
        if (_completionSource == null)
            return;

        IsVisible = false;
        IsHitTestVisible = false;

        _callstackPanel.Children.Clear();
        _currentCallstack = string.Empty;

        _completionSource.TrySetResult(null);
        _completionSource = null;
    }

    void BuildCallstackEntries(string callstack)
    {
        _callstackPanel.Children.Clear();

        if (string.IsNullOrWhiteSpace(callstack))
            return;

        var lines = callstack.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var line = rawLine.TrimEnd('\r');
            AddCallstackLine(line);
        }
    }

    void AddCallstackLine(string line)
    {
        if (TryParseCallstackLine(line, out var entry))
        {
            var textBlock = CreateCallstackTextBlock(line, HyperlinkBrush);
            textBlock.TextDecorations = TextDecorations.Underline;
            textBlock.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(textBlock, entry.FilePath);

            textBlock.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(textBlock).Properties.IsLeftButtonPressed == false)
                    return;

                e.Handled = true;
                TryOpenCallstackEntry(entry);
            };

            _callstackPanel.Children.Add(textBlock);
            return;
        }

        var defaultLine = CreateCallstackTextBlock(line, DefaultLineBrush);
        _callstackPanel.Children.Add(defaultLine);
    }

    static TextBlock CreateCallstackTextBlock(string text, IBrush foreground)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = CallstackFont,
            Margin = LineMargin
        };

        return block;
    }

    static bool TryParseCallstackLine(string line, out CallstackEntry entry)
    {
        entry = default;

        var inIndex = line.LastIndexOf(" in ", StringComparison.Ordinal);
        if (inIndex < 0)
            return false;

        var fileAndLine = line.Substring(inIndex + 4);
        var lineIndex = fileAndLine.LastIndexOf(":line ", StringComparison.Ordinal);
        if (lineIndex < 0)
            return false;

        var path = fileAndLine[..lineIndex].Trim();
        var numberText = fileAndLine[(lineIndex + 6)..].Trim();

        if (int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber) == false)
            return false;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        entry = new CallstackEntry(path, lineNumber);
        return true;
    }

    static bool TryOpenCallstackEntry(CallstackEntry entry)
    {
        if (File.Exists(entry.FilePath) == false)
            return false;

        if (entry.LineNumber > 0 && TryOpenWithCode(entry.FilePath, entry.LineNumber))
            return true;

        return TryOpenWithShell(entry.FilePath);
    }

    static bool TryOpenWithCode(string path, int lineNumber)
    {
        var arguments = string.Format(CultureInfo.InvariantCulture,
            "--goto \"{0}:{1}\"",
            path,
            lineNumber);

        var startInfo = new ProcessStartInfo
        {
            FileName = "code",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return TryStartProcess(startInfo);
    }

    static bool TryOpenWithShell(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        return TryStartProcess(startInfo);
    }

    static bool TryStartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            return process != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    readonly struct CallstackEntry
    {
        public CallstackEntry(string filePath, int lineNumber)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
        }

        public string FilePath { get; }

        public int LineNumber { get; }
    }
}
