using System;

namespace Common.Scripts
{
    public sealed class LogEvent
    {
        public LogEvent(string title, string message, LogLevel level, TimeSpan? duration, string? callstack)
        {
            Title = title;
            Message = message;
            Level = level;
            Duration = duration;
            Timestamp = DateTimeOffset.UtcNow;
            Callstack = callstack ?? string.Empty;
        }

        public string Title { get; }

        public string Message { get; }

        public LogLevel Level { get; }

        public TimeSpan? Duration { get; }

        public DateTimeOffset Timestamp { get; }

        public string Callstack { get; }
    }
}
