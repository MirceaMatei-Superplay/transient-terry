using Common.Scripts;
using SetupRunner = SubmodulesFlattenerSetup.Scripts.SubmodulesFlattenerSetup;

namespace SubmodulesFlattenerSetup.Apps;

static class SubmodulesFlattenerSetupApp
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine(
                "Usage: SubmodulesFlattenerSetupApp <unityPath> <sourceRepoUrl> <targetRepoUrl> <sourceBranch> <targetBranch>");
            return 1;
        }

        var unityPath = args[0];
        var sourceRepoUrl = args[1];
        var targetRepoUrl = args[2];
        var sourceBranch = args[3];
        var targetBranch = args[4];

        if (File.Exists(unityPath) == false)
        {
            Console.WriteLine($"Unity '{unityPath}' does not exist");
            return 1;
        }

        var pat = Environment.GetEnvironmentVariable(Texts.GH_PAT_ENV);
        var setup = new SetupRunner(unityPath, sourceRepoUrl,
            targetRepoUrl, sourceBranch, targetBranch, pat);

        var isSetupCompleted = true;

        try
        {
            isSetupCompleted = await setup.Run();
        }
        catch (Exception e)
        {
            Logger.Write(e.ToString());
            throw;
        }

        if (isSetupCompleted)
            Logger.Write(Texts.SETUP_FINISHED);

        return 0;
    }
}
