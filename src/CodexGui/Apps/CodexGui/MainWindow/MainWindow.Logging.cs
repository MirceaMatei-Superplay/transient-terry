using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Common.Scripts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    void InitializeLogging()
    {
        _logItemsControl.ItemsSource = _logEntries;
        _summaryItemsControl.ItemsSource = _summaryEntries;
        UpdateSummaryText();

        Logger.LogGenerated += HandleLogGenerated;
        Closed += OnClosed;
    }

    void HandleLogGenerated(LogEvent logEvent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var entry = new LogDisplayEntry(logEvent);
            _logEntries.Add(entry);

            if (logEvent.Duration.HasValue)
            {
                _completedLogEntries.Add(entry);
                UpdateSummary();
            }

            UpdateSummaryText();
            ScrollLogToEnd();
        });
    }

    void UpdateSummary()
    {
        var ordered = _completedLogEntries
            .OrderByDescending(log => log.DurationMilliseconds)
            .ToList();

        var maxDuration = ordered.FirstOrDefault()?.DurationMilliseconds ?? 0;
        var barWidth = _summaryBarWidth > 0
            ? _summaryBarWidth
            : DEFAULT_SUMMARY_BAR_WIDTH;

        _summaryEntries.Clear();
        foreach (var entry in ordered)
            _summaryEntries.Add(new LogSummaryEntry(entry, maxDuration, barWidth));
    }

    void UpdateSummaryText()
    {
        if (_logSummaryText == null)
            return;

        var totalEvents = _logEntries.Count;
        if (_completedLogEntries.Count == 0)
        {
            _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} events logged",
                totalEvents);
            return;
        }

        var ordered = _completedLogEntries
            .Where(entry => entry.DurationMilliseconds > 0)
            .OrderBy(entry => entry.DurationMilliseconds)
            .ToList();

        if (ordered.Count == 0)
        {
            _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} events logged",
                totalEvents);
            return;
        }

        var shortest = ordered.First();
        var longest = ordered.Last();

        _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
            "{0} events • Longest: {1} ({2}) • Shortest: {3} ({4})",
            totalEvents,
            longest.SummaryLabel,
            FormatDuration(TimeSpan.FromMilliseconds(longest.DurationMilliseconds)),
            shortest.SummaryLabel,
            FormatDuration(TimeSpan.FromMilliseconds(shortest.DurationMilliseconds)));
    }

    void ScrollLogToEnd()
    {
        if (_logScrollViewer == null)
            return;

        var extent = _logScrollViewer.Extent;
        _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, extent.Height);
    }

    void OnClosed(object? sender, EventArgs e)
    {
        Logger.LogGenerated -= HandleLogGenerated;
        _summaryBoundsSubscription?.Dispose();
        _messageOverlay.CallstackRequested -= OnMessageCallstackRequested;
    }

    static string FormatDuration(TimeSpan duration)
    {
        var totalSeconds = duration.TotalSeconds;

        if (Math.Abs(totalSeconds) >= 60)
        {
            var minutes = (int)(totalSeconds / 60);
            var secondsRemainder = totalSeconds - (minutes * 60);

            var minuteLabel = minutes == 1
                ? "1 min"
                : string.Format(CultureInfo.InvariantCulture, "{0} min", minutes);

            var secondsLabel = GetSecondsLabel(secondsRemainder);

            return string.Format(CultureInfo.InvariantCulture,
                "{0}, {1}",
                minuteLabel,
                secondsLabel);
        }

        return GetSecondsLabel(totalSeconds);
    }

    static string GetSecondsLabel(double seconds)
    {
        if (Math.Abs(seconds) < 1e-9)
            return "0 seconds";

        if (Math.Abs(seconds - 1) < 1e-9)
            return "1 second";

        return string.Format(CultureInfo.InvariantCulture,
            "{0:0.##} seconds",
            seconds);
    }

    sealed class LogDisplayEntry : INotifyPropertyChanged
    {
        const int SUMMARY_LABEL_LENGTH = 96;
        const int COLLAPSED_MESSAGE_MAX_LINES = 10;
        const char COLLAPSED_SUFFIX = '…';

        static readonly IBrush InfoBackgroundBrush = new SolidColorBrush(Color.Parse("#1F1F2A"));
        static readonly IBrush InfoBorderBrush = new SolidColorBrush(Color.Parse("#2D2D3A"));
        static readonly IBrush InfoAccentBrush = new SolidColorBrush(Color.Parse("#4D8DFF"));

        static readonly IBrush SuccessBackgroundBrush = new SolidColorBrush(Color.Parse("#14241B"));
        static readonly IBrush SuccessBorderBrush = new SolidColorBrush(Color.Parse("#205136"));
        static readonly IBrush SuccessAccentBrush = new SolidColorBrush(Color.Parse("#32C671"));

        static readonly IBrush WarningBackgroundBrush = new SolidColorBrush(Color.Parse("#2C2513"));
        static readonly IBrush WarningBorderBrush = new SolidColorBrush(Color.Parse("#5A4C22"));
        static readonly IBrush WarningAccentBrush = new SolidColorBrush(Color.Parse("#E0B341"));

        static readonly IBrush ErrorBackgroundBrush = new SolidColorBrush(Color.Parse("#2C1B1B"));
        static readonly IBrush ErrorBorderBrush = new SolidColorBrush(Color.Parse("#5A2A2A"));
        static readonly IBrush ErrorAccentBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));

        public LogDisplayEntry(LogEvent logEvent)
        {
            Title = logEvent.Title;
            Message = logEvent.Message ?? string.Empty;
            Level = logEvent.Level;
            Duration = logEvent.Duration;
            Timestamp = logEvent.Timestamp;
            Callstack = string.IsNullOrWhiteSpace(logEvent.Callstack)
                ? string.Empty
                : logEvent.Callstack.Trim();

            var normalizedMessage = NormalizeLineEndings(Message);
            var lines = normalizedMessage.Length == 0
                ? Array.Empty<string>()
                : normalizedMessage.Split('\n');

            _hasExpandableMessage = lines.Length > COLLAPSED_MESSAGE_MAX_LINES;
            _collapsedMessage = _hasExpandableMessage
                ? BuildCollapsedMessage(lines)
                : Message;
        }

        public string Title { get; }

        public string Message { get; }

        public string DisplayMessage
        {
            get
            {
                if (_hasExpandableMessage == false || _isMessageExpanded)
                    return Message;

                return _collapsedMessage;
            }
        }

        public LogLevel Level { get; }

        public TimeSpan? Duration { get; }

        public DateTimeOffset Timestamp { get; }

        public string Callstack { get; }

        public bool HasCallstack => string.IsNullOrWhiteSpace(Callstack) == false;

        public string DurationDisplay
            => Duration.HasValue
                ? FormatDuration(Duration.Value)
                : string.Empty;

        public string TimestampText
            => Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        public double DurationMilliseconds => Duration?.TotalMilliseconds ?? 0;

        public string CopyText
        {
            get
            {
                var builder = new StringBuilder();
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} - {1}",
                    Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Title));

                if (Duration.HasValue)
                    builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "Duration: {0}",
                        FormatDuration(Duration.Value)));

                if (string.IsNullOrWhiteSpace(Message) == false)
                    builder.AppendLine(Message);

                return builder.ToString().TrimEnd();
            }
        }

        public string SummaryLabel
        {
            get
            {
                if (Title.Length <= SUMMARY_LABEL_LENGTH)
                    return Title;

                return string.Format(CultureInfo.InvariantCulture,
                    "{0}…",
                    Title.Substring(0, SUMMARY_LABEL_LENGTH - 1));
            }
        }

        public IBrush BackgroundBrush => Level switch
        {
            LogLevel.Success => SuccessBackgroundBrush,
            LogLevel.Warning => WarningBackgroundBrush,
            LogLevel.Error => ErrorBackgroundBrush,
            _ => InfoBackgroundBrush
        };

        public IBrush BorderBrush => Level switch
        {
            LogLevel.Success => SuccessBorderBrush,
            LogLevel.Warning => WarningBorderBrush,
            LogLevel.Error => ErrorBorderBrush,
            _ => InfoBorderBrush
        };

        public IBrush AccentBrush => Level switch
        {
            LogLevel.Success => SuccessAccentBrush,
            LogLevel.Warning => WarningAccentBrush,
            LogLevel.Error => ErrorAccentBrush,
            _ => InfoAccentBrush
        };

        public string ToggleButtonLabel => _isMessageExpanded ? "Collapse" : "Expand";

        public bool ShowToggleButton => _hasExpandableMessage;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void ToggleMessageExpansion()
        {
            if (_hasExpandableMessage == false)
                return;

            _isMessageExpanded = !_isMessageExpanded;

            NotifyPropertyChanged(nameof(DisplayMessage));
            NotifyPropertyChanged(nameof(ToggleButtonLabel));
        }

        static string NormalizeLineEndings(string message)
            => message
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

        static string BuildCollapsedMessage(IReadOnlyList<string> lines)
        {
            var builder = new StringBuilder();

            for (var index = 0; index < COLLAPSED_MESSAGE_MAX_LINES; index++)
            {
                if (index > 0)
                    builder.AppendLine();

                builder.Append(lines[index]);
            }

            builder.AppendLine();
            builder.Append(COLLAPSED_SUFFIX);

            return builder.ToString();
        }

        void NotifyPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        readonly bool _hasExpandableMessage;
        readonly string _collapsedMessage = string.Empty;
        bool _isMessageExpanded;
    }

    sealed class LogSummaryEntry
    {
        const double MIN_WIDTH = 6;

        public LogSummaryEntry(LogDisplayEntry entry, double maxDuration, double maxWidth)
        {
            Title = entry.Title;
            FullTitle = entry.Title;
            DurationDisplay = entry.DurationDisplay;
            AccentBrush = entry.AccentBrush;
            GraphWidth = CalculateWidth(entry.DurationMilliseconds, maxDuration, maxWidth);
        }

        public string Title { get; }

        public string FullTitle { get; }

        public string DurationDisplay { get; }

        public IBrush AccentBrush { get; }

        public double GraphWidth { get; }

        public string CopyText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DurationDisplay))
                    return Title;

                return string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1})",
                    Title,
                    DurationDisplay);
            }
        }

        static double CalculateWidth(double duration, double maxDuration, double maxWidth)
        {
            if (maxWidth <= 0)
                maxWidth = DEFAULT_SUMMARY_BAR_WIDTH;

            if (duration <= 0 || maxDuration <= 0)
                return MIN_WIDTH;

            var ratio = duration / maxDuration;
            return Math.Max(MIN_WIDTH, ratio * maxWidth);
        }
    }
}
