namespace Common.Scripts
{
    public class GitException : Exception
    {
        public int ExitCode { get; }

        public GitException(string message, int exitCode) : base(message)
        {
            ExitCode = exitCode;
        }
    }
}
