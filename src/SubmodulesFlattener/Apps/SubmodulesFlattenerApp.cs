using Common.Scripts;
using System.Threading.Tasks;

namespace SubmodulesFlattener.Apps;

static class SubmodulesFlattenerApp
{
    static async Task<int> Main(string[] args)
    {
        var repoPath = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        if (Directory.Exists(repoPath) == false)
        {
            Console.WriteLine($"Path '{repoPath}' does not exist");
            return 1;
        }

        var sourceBranch = args[1];
        var targetBranch = args[2];

        var pat = Environment.GetEnvironmentVariable(Texts.GH_PAT_ENV);
        var flattener = new Common.Scripts.SubmodulesFlattener(pat);
        try
        {
            await flattener.Run(repoPath, sourceBranch, targetBranch);
        }
        catch (Exception e)
        {
            Logger.Write(e.ToString());
            throw;
        }

        return 0;
    }
}
