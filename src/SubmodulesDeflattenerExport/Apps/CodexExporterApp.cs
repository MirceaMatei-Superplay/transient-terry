using Common.Scripts;
using ExportTool = SubmodulesDeflattenerExport.Scripts.SubmodulesDeflattenerExport;

namespace SubmodulesDeflattenerExport.Apps
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 4)
            {
                Logger.Write(Texts.USAGE);
                return 1;
            }

            var sourceRepoUrl = args[0];
            var targetRepoUrl = args[1];
            var sourceBranch = args[2];
            var targetBranch = args[3];
            Logger.Write(string.Format(Texts.STARTING_REWIRING_TEMPLATE, targetRepoUrl));

            var pat = Environment.GetEnvironmentVariable(Texts.GH_PAT_ENV);
            var tool = new ExportTool(pat, sourceRepoUrl, targetRepoUrl, sourceBranch, targetBranch);

            string mainPrUrl;
            try
            {
                mainPrUrl = await tool.Run();
            }
            catch (Exception e)
            {
                Logger.Write(e.ToString());
                throw;
            }

            Logger.Write(string.Format(Texts.MAIN_PR_LINK_TEMPLATE, mainPrUrl));

            Logger.Write(Texts.REWIRING_FINISHED);
            return 0;
        }
    }
}

