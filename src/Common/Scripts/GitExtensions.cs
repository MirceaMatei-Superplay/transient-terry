using System.Threading.Tasks;

namespace Common.Scripts
{
    public static class GitExtensions
    {
        public static async Task<int> SuppressGitException(this Task<int> task)
        {
            try
            {
                return await task;
            }
            catch (GitException ex)
            {
                return ex.ExitCode;
            }
        }
    }
}
