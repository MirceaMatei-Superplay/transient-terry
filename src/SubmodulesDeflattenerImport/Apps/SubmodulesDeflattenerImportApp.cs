using System;
using Common.Scripts;
using SubmodulesDeflattenerImport.Scripts;
using ImportTool = SubmodulesDeflattenerImport.Scripts.SubmodulesDeflattenerImport;

namespace SubmodulesDeflattenerImport.Apps
{
    internal static class SubmodulesDeflattenerImportApp
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 3)
            {
                Logger.Write(Texts.IMPORT_USAGE);
                return 1;
            }

            var repoUrl = args[0];
            var sourceBranch = args[1];
            var targetBranch = args[2];
            Logger.Write(string.Format(Texts.STARTING_REWIRING_TEMPLATE, repoUrl));

            var pat = Environment.GetEnvironmentVariable(Texts.GH_PAT_ENV);
            var tool = new ImportTool(pat, repoUrl, targetBranch, sourceBranch);

            try
            {
                await tool.Run();
            }
            catch (Exception e)
            {
                Logger.Write(e.ToString());
                throw;
            }

            Logger.Write(Texts.REWIRING_FINISHED);
            return 0;
        }
    }
}
