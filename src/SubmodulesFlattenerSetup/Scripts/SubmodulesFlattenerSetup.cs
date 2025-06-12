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

        var runtime = PrepareRuntime();
        var repoName = RepoUtils.GetRepoName(targetRepoUrl);
        _repoPath = Path.Combine(runtime, repoName);
    }

    public async Task Run()
    {
        Helpers.PrepareRuntime();
        
        await CloneRepository(_targetRepoUrl, _repoPath);
        // Moving to unique temp so that we avoid error 128 while fetching the branch.
        var tempBranch = $"temp-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await Helpers.CreateAndCheckoutBranch(_repoPath, tempBranch, _pat, true);
        await FetchBranch(_repoPath, _sourceRepoUrl, _sourceBranch);

        var flattener = new SubmodulesFlattener(_pat);
        await flattener.Run(_repoPath, _sourceBranch, _targetBranch);

        var regen = new CsprojRegen(_unityPath, _repoPath);
        await regen.Run();

        var tool = new CsprojSetupTool(_repoPath);
        await tool.Setup();
    }

    static string PrepareRuntime()
    {
        Logger.Write(Texts.PREPARING_RUNTIME_FOLDER);
        if (Directory.Exists(Texts.RUNTIME_FOLDER))
            Helpers.DeleteDirectory(Texts.RUNTIME_FOLDER);

        Directory.CreateDirectory(Texts.RUNTIME_FOLDER);
        var path = Path.GetFullPath(Texts.RUNTIME_FOLDER);
        Logger.Write(string.Format(Texts.RUNTIME_FOLDER_READY, path));
        return path;
    }

    async Task CloneRepository(string url, string path)
    {
        Logger.Write(string.Format(Texts.CLONING_REPOSITORY, url, path));
        await Helpers.RunGit(string.Format(Texts.CLONE_COMMAND, url, path), _pat);
    }

    async Task FetchBranch(string path, string url, string branch)
    {
        Logger.Write(string.Format("Fetching {0} from {1}", branch, url));
        await Helpers.RunGit($"-C {path} fetch {url} {branch}:{branch}", _pat);
    }
}
