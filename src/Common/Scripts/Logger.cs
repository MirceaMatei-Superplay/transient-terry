using System.Diagnostics;

namespace Common.Scripts
{
    public static class Logger
    {
        [Conditional("DEBUG")]
        public static void Write(string message) => Console.WriteLine(message);
    }
}
