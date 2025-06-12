using Common.Scripts;
using System.Threading.Tasks;

namespace CsprojSetupToolApp.Apps;

public static class CsprojSetupToolApp
{
    public static async Task Run(string unityPath, string repoPath, bool runUnity, bool makeCommits = true, bool pushWhenDone = true)
    {
        if (runUnity)
        {
            var regen = new CsprojRegen(unityPath, repoPath);
            await regen.Run(makeCommits, pushWhenDone);
        }

        var tool = new CsprojSetupTool(repoPath);
        await tool.Setup(makeCommits, pushWhenDone);
        Logger.Write(Texts.SETUP_FINISHED);
    }

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: CsprojSetupToolApp <unityPath> [repoPath]");
            return 1;
        }

        var unityPath = args[0];
        var repoPath = args.Length > 1 ? args[1] : Environment.CurrentDirectory;
        if (File.Exists(unityPath) == false)
        {
            Console.WriteLine($"Unity '{unityPath}' does not exist");
            return 1;
        }
        if (Directory.Exists(repoPath) == false)
        {
            Console.WriteLine($"Path '{repoPath}' does not exist");
            return 1;
        }

        await Run(unityPath, repoPath, true);

        return 0;
    }
}
