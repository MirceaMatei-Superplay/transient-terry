using System;

namespace Common.Scripts
{
    public static class Logger
    {
        public static event Action<LogEvent>? LogGenerated;

        public static void Write(string message)
            => Publish(new LogEvent("Log", message, LogLevel.Info, null));

        public static void LogInfo(string title, string message)
            => Publish(new LogEvent(title, message, LogLevel.Info, null));

        public static void LogWarning(string title, string message)
            => Publish(new LogEvent(title, message, LogLevel.Warning, null));

        public static void LogOperationResult(string title, string message, TimeSpan duration, LogLevel level)
        {
            var formatted = string.Format("{0} • Finished in {1:F0} ms", message.Trim(), duration.TotalMilliseconds);
            Publish(new LogEvent(title, formatted, level, duration));
        }

        static void Publish(LogEvent logEvent)
        {
#if DEBUG
            Console.WriteLine(logEvent.Message);
#endif
            LogGenerated?.Invoke(logEvent);
        }
    }
}
