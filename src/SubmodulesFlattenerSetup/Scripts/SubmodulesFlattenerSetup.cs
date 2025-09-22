using Common.Scripts;

namespace SubmodulesFlattenerSetup.Scripts;

public class SubmodulesFlattenerSetup
{
    readonly string _unityPath;
    readonly string _sourceRepoUrl;
    readonly string _targetRepoUrl;
    readonly string _sourceBranch;
    readonly string _targetBranch;
    readonly string? _pat;
    readonly string _repoPath;

    public SubmodulesFlattenerSetup(string unityPath, string sourceRepoUrl,
        string targetRepoUrl, string sourceBranch, string targetBranch, string? pat)
    {
        _unityPath = unityPath;
        _sourceRepoUrl = sourceRepoUrl;
        _targetRepoUrl = targetRepoUrl;
        _sourceBranch = sourceBranch;
        _targetBranch = targetBranch;
        _pat = pat;

        var runtime = Helpers.PrepareRuntime();
        var repoName = RepoUtils.GetRepoName(targetRepoUrl);
        _repoPath = Path.Combine(runtime, Texts.REMOTE_SETUP_FOLDER, Texts.TARGET_FOLDER, repoName);
    }

    public async Task<bool> Run()
    {
        await CloneRepository(_targetRepoUrl, _repoPath);
        // Moving to unique temp so that we avoid error 128 while fetching the branch.
        var tempBranch = $"temp-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await Helpers.CreateAndCheckoutBranch(_repoPath, tempBranch, _pat, true);
        await FetchBranch(_repoPath, _sourceRepoUrl, _sourceBranch);

        var flattener = new SubmodulesFlattener(_pat);
        var isFlattened = await flattener.Run(_repoPath, _sourceBranch, _targetBranch);
        if (isFlattened == false)
        {
            Logger.Write("Submodules flattening skipped because the target branch already exists. " +
                "Delete the branch and rerun to continue setup.");
            return false;
        }

        var regen = new CsprojRegen(_unityPath, _repoPath);
        await regen.Run();

        var tool = new CsprojSetupTool(_repoPath);
        await tool.Setup();

        return true;
    }

    async Task CloneRepository(string url, string path)
        => await Helpers.PrepareRepositoryCache(url, path, _pat);

    async Task FetchBranch(string path, string url, string branch)
    {
        Logger.Write(string.Format("Fetching {0} from {1}", branch, url));
        await Helpers.RunGit($"-C {path} fetch --depth 1 --force {url} {branch}:{branch}", _pat);
    }
}
