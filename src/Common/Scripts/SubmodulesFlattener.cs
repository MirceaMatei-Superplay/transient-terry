using Common.Scripts;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Scripts;

public class SubmodulesFlattener
{
    private readonly string? _pat;

    public SubmodulesFlattener(string? pat)
    {
        _pat = pat;
    }

    public async Task<bool> Run(string repoPath, string sourceBranch, string targetBranch)
    {
        var localBranch = await Helpers.RunGitCapture($"-C {repoPath} branch --list {targetBranch}", _pat);
        if (string.IsNullOrEmpty(localBranch) == false)
        {
            var remoteBranch = await Helpers.RunGitCapture(
                $"-C {repoPath} ls-remote --heads {Texts.ORIGIN_REMOTE} {targetBranch}",
                _pat);

            if (string.IsNullOrEmpty(remoteBranch))
            {
                Logger.Write(string.Format(Texts.DELETING_LOCAL_BRANCH_WITHOUT_REMOTE, targetBranch));
                await Helpers.RunGit($"-C {repoPath} branch -D {targetBranch}", _pat);
            }
            else
            {
                Logger.Write(string.Format(Texts.BRANCH_EXISTS, targetBranch));
                return false;
            }
        }

        await Helpers.RunGit($"-C {repoPath} reset --hard", _pat);
        await Helpers.RunGit($"-C {repoPath} clean -fd", _pat);
        await CheckoutBranch(repoPath, sourceBranch);

        HandleGitignoreFields(repoPath);

        await Flatten(repoPath);

        await Helpers.RunGit($"-C {repoPath} checkout -b {targetBranch}", _pat);
        await Helpers.RunGit($"-C {repoPath} add -A", _pat);
        await Helpers.RunGit($"-C {repoPath} commit -m \"Flatten submodules\"", _pat);
        await Helpers.RunGit($"-C {repoPath} push -f origin {targetBranch}", _pat);

        // TODO: Figure out how to eventually merge this branch into the codex main branch.
        // CheckoutBranch(repoPath, Texts.CODEX_MAIN_BRANCH);
        // var mergeExitCode = RunGit($"-C {repoPath} merge {targetBranch}");
        // if (mergeExitCode == 0)
        //     RunGit($"-C {repoPath} push origin {Texts.CODEX_MAIN_BRANCH}");

        return true;
    }

    private async Task Flatten(string repoPath)
    {
        var submodules = Helpers.ParseSubmodules(repoPath, _pat).ToList();

        foreach (var submodule in submodules)
        {
            var submodulePath = Path.Combine(repoPath, submodule.Path);

            await Helpers.RunGit($"-C {repoPath} submodule update --init --recursive {submodule.Path}", _pat);

            var modulesFile = Path.Combine(submodulePath, Texts.DOT_GITMODULES);
            if (File.Exists(modulesFile))
                await Flatten(submodulePath);

            await Helpers.RunGit($"-C {repoPath} rm --cached {submodule.Path}", _pat);

            await Helpers.RunGit($"-C {repoPath} config -f .gitmodules --remove-section submodule.{submodule.Name}", _pat);
            await Helpers.RunGit($"-C {repoPath} config --remove-section submodule.{submodule.Name}", _pat);

            var submoduleGit = Path.Combine(submodulePath, ".git");
            if (Directory.Exists(submoduleGit))
                Helpers.DeleteDirectory(submoduleGit);
            else if (File.Exists(submoduleGit))
                File.Delete(submoduleGit);

            var modulesDir = Path.Combine(repoPath, ".git", "modules", submodule.Path);
            if (Directory.Exists(modulesDir))
                Helpers.DeleteDirectory(modulesDir);

            await Helpers.RunGit($"-C {repoPath} add {Texts.DOT_GITMODULES} {submodule.Path}", _pat);

            Helpers.DeleteGitIgnore(submodulePath);
        }

        var gitmodulesPath = Path.Combine(repoPath, Texts.DOT_GITMODULES);
        if (File.Exists(gitmodulesPath) && Helpers.ParseSubmodules(repoPath, _pat).Any() == false)
        {
            File.Delete(gitmodulesPath);
            await Helpers.RunGit($"-C {repoPath} add {Texts.DOT_GITMODULES}", _pat);
        }

    }

    private static void HandleGitignoreFields(string repoPath)
    {
        var gitIgnorePath = Path.Combine(repoPath, ".gitignore");
        if (File.Exists(gitIgnorePath) == false)
            return;

        var lines = File.ReadAllLines(gitIgnorePath);
        var filtered = lines.Where(l => l.Trim() != "*.csproj" && l.Trim() != "*.sln").ToArray();
        if (filtered.Length != lines.Length)
            File.WriteAllLines(gitIgnorePath, filtered);
    }
    


    private async Task CheckoutBranch(string path, string branch)
    {
        await Helpers.EnsureBranchAvailability(path, branch, _pat);

        Logger.Write(string.Format(Texts.CHECKING_OUT_STATUS, branch, path));
        await Helpers.RunGit(string.Format(Texts.CHECKING_OUT, path, branch), _pat);
    }
}
