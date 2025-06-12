using Octokit;
using Common.Scripts;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SubmodulesDeflattenerExport.Scripts
{
    public class SubmodulesDeflattenerExport
    {
        // TODO: refactor all usages of _targetRepoUrl, _mainRepoPath so that we never
        // pass them as method parameters, always use the class properties.
        private readonly string? _pat;
        private readonly string _sourceRepoUrl;
        private readonly string _targetRepoUrl;
        private readonly string _mainRepoPath;
        private readonly string _sourceBranch;
        private readonly string _targetBranch;
        private readonly Dictionary<string, string> _baseRefs;
        private readonly Dictionary<string, string> _newBranches;

        public SubmodulesDeflattenerExport(string? pat, string sourceRepoUrl,
            string targetRepoUrl, string sourceBranch, string targetBranch,
            Dictionary<string, string>? baseRefs = null,
            Dictionary<string, string>? newBranches = null)
        {
            _pat = pat;
            _sourceRepoUrl = sourceRepoUrl;
            _targetRepoUrl = targetRepoUrl;
            _sourceBranch = sourceBranch;
            _targetBranch = targetBranch;
            _baseRefs = baseRefs ?? new Dictionary<string, string>();
            _newBranches = newBranches ?? new Dictionary<string, string>();

            var runtime = Helpers.PrepareRuntime();

            var repoName = RepoUtils.GetRepoName(targetRepoUrl);
            Logger.Write(string.Format(Texts.REPOSITORY_NAME_RESOLVED, repoName));

            _mainRepoPath = Path.Combine(runtime, repoName);
        }

        async Task<string?> PrepareMainRepo(bool checkDiffs)
        {
            await CloneRepository(_targetRepoUrl, _mainRepoPath);

            await CheckoutBranch(_mainRepoPath, _targetBranch);

            BackupFiles(_mainRepoPath);

            await FetchBranch(_mainRepoPath, _sourceRepoUrl, _sourceBranch);
            await CheckoutBranch(_mainRepoPath, _sourceBranch);

            string? branch = null;
            if (checkDiffs == false)
            {
                var nextPr = await GetNextPrNumber(_targetRepoUrl);
                Logger.Write(string.Format(Texts.NEXT_PR_NUMBER, nextPr));

                branch = string.Format(Texts.TEMP_REWIRE_BRANCH_TEMPLATE, nextPr);
                await Helpers.CreateAndCheckoutBranch(_mainRepoPath, branch, _pat, true);
            }

            RemoveUnnecessaryFiles(_mainRepoPath);
            MoveSharedFolder(_mainRepoPath);
            DeleteGitFiles(_mainRepoPath);
            RestoreGitFiles(_mainRepoPath);

            if (checkDiffs)
                await Commit(_mainRepoPath, 1, Texts.REMOVE_CODEX_FILES_DETAILS);
            else if (branch != null)
                await CommitAndPush(_mainRepoPath, branch, 1, Texts.REMOVE_CODEX_FILES_DETAILS);

            return branch;
        }

        public async Task<string> Run()
        {
            var branch = await PrepareMainRepo(false) ?? string.Empty;

            var mergeBase = await Helpers.GetMergeBase(_mainRepoPath, _targetBranch, _pat);

            var submodulePrs = await ProcessSubmodules(_mainRepoPath, mergeBase, string.Empty, false);

            Helpers.SanitizeGitModules(_mainRepoPath, _pat);

            RestoreSharedMetaFiles(_mainRepoPath);
            await CommitAndPush(_mainRepoPath, branch, 2, Texts.UPDATE_SUBMODULES_DETAILS);

            var mainPr = await CreateMainPr(_targetRepoUrl, branch, submodulePrs);
            await AddMainPrLinkToSubmodules(mainPr.HtmlUrl, submodulePrs);

            await Helpers.DeleteRemoteBranchIfExists(_sourceRepoUrl, _sourceBranch, _pat);

            return mainPr.HtmlUrl;
        }

        private async Task CloneRepository(string url, string path)
        {
            Logger.Write(string.Format(Texts.CLONING_REPOSITORY, url, path));
            await Helpers.RunGit(string.Format(Texts.CLONE_COMMAND, url, path), _pat);
        }

        private async Task CheckoutBranch(string path, string branch)
        {
            Logger.Write(string.Format(Texts.CHECKING_OUT_STATUS, branch, path));
            await Helpers.RunGit(string.Format(Texts.CHECKING_OUT, path, branch), _pat);
        }

        private async Task FetchBranch(string path, string url, string branch)
        {
            Logger.Write(string.Format("Fetching {0} from {1}", branch, url));
            await Helpers.RunGit($"-C {path} fetch {url} {branch}:{branch}", _pat);
        }

        private static void BackupFiles(string path)
        {
            Logger.Write(Texts.BACKING_UP_FILES);
            var temp = Path.Combine(Texts.RUNTIME_FOLDER, Texts.TEMP_FOLDER);
            Directory.CreateDirectory(temp);
            File.Copy(Path.Combine(path, Texts.DOT_GITIGNORE),
                Path.Combine(temp, Texts.DOT_GITIGNORE), true);
            var modules = Path.Combine(path, Texts.DOT_GITMODULES);
            if (File.Exists(modules))
                File.Copy(modules, Path.Combine(temp, Texts.DOT_GITMODULES), true);

            var sharedModules = Path.Combine(path,
                Texts.ASSETS_FOLDER,
                Texts.SHARED_FOLDER,
                Texts.DOT_GITMODULES);
            if (File.Exists(sharedModules))
                File.Copy(sharedModules,
                    Path.Combine(temp, Texts.SHARED_GITMODULES_TEMP), true);
        }

        private static (string owner, string name) ParseOwnerAndName(string url)
        {
            var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
            return (segments[0], segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1]);
        }

        private async Task<int> GetNextPrNumber(string repoUrl)
        {
            var (owner, name) = ParseOwnerAndName(repoUrl);
            var client = new GitHubClient(new ProductHeaderValue(Texts.GITHUB_CLIENT_PRODUCT));
            if (!string.IsNullOrEmpty(_pat))
                client.Credentials = new Credentials(_pat);

            var request = new PullRequestRequest { State = ItemStateFilter.All };
            var prs = await client.PullRequest.GetAllForRepository(owner, name, request);
            var max = 0;
            foreach (var pr in prs)
                if (pr.Number > max)
                    max = pr.Number;

            return max + 1;
        }

        private static void RemoveUnnecessaryFiles(string repoPath)
        {
            var files = new[]
            {
                Texts.DOT_GITIGNORE,
                Texts.DIRECTORY_BUILD_PROPS_FILE
            };
            foreach (var file in files)
            {
                var fullPath = Path.Combine(repoPath, file);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }

            foreach (var sln in Directory.GetFiles(repoPath, Texts.SOLUTION_FILES_PATTERN))
                File.Delete(sln);

            foreach (var csproj in Directory.GetFiles(repoPath, "*.csproj"))
                File.Delete(csproj);

            var assemblies = Path.Combine(repoPath, Texts.UNITY_ASSEMBLIES_FOLDER);
            if (Directory.Exists(assemblies))
                Helpers.DeleteDirectory(assemblies);
        }

        private static void MoveSharedFolder(string repoPath)
        {
            var source = Path.Combine(repoPath, Texts.ASSETS_FOLDER, Texts.SHARED_FOLDER);
            if (Directory.Exists(source) == false)
                return;

            var dest = Path.Combine(Texts.RUNTIME_FOLDER, Texts.TEMP_FOLDER, Texts.SHARED_COPY_FOLDER);
            Helpers.CopyDirectory(source, dest);

            Helpers.DeleteDirectory(source);
        }

        private static void DeleteGitFiles(string repoPath)
        {
            foreach (var file in new[] { Texts.DOT_GITIGNORE, Texts.DOT_GITMODULES })
            {
                var fullPath = Path.Combine(repoPath, file);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
        }

        private static void RestoreGitFiles(string repoPath)
        {
            var temp = Path.Combine(Texts.RUNTIME_FOLDER, Texts.TEMP_FOLDER);
            foreach (var file in new[] { Texts.DOT_GITIGNORE, Texts.DOT_GITMODULES })
            {
                var source = Path.Combine(temp, file);
                var dest = Path.Combine(repoPath, file);
                if (File.Exists(source))
                    File.Copy(source, dest, true);
            }
        }

        private void SanitizeGitModules(string repoPath) => Helpers.SanitizeGitModules(repoPath, _pat);


        private static void RestoreSharedMetaFiles(string repoPath)
        {
            var source = Path.Combine(Texts.RUNTIME_FOLDER, Texts.TEMP_FOLDER, Texts.SHARED_COPY_FOLDER);
            var dest = Path.Combine(repoPath, Texts.ASSETS_FOLDER, Texts.SHARED_FOLDER);

            if (Directory.Exists(source) == false)
                throw new Exception(Texts.SHARED_COPY_MISSING_ERROR);

            foreach (var file in Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }

            var sharedModulesSource = Path.Combine(Texts.RUNTIME_FOLDER,
                Texts.TEMP_FOLDER, Texts.SHARED_GITMODULES_TEMP);
            var sharedModulesDest = Path.Combine(dest, Texts.DOT_GITMODULES);
            if (File.Exists(sharedModulesSource))
            {
                Directory.CreateDirectory(dest);
                File.Copy(sharedModulesSource, sharedModulesDest, true);
            }
        }






        private async Task<List<PrInfo>> ProcessSubmodules(
            string repoPath,
            string baseCommit,
            string sharedRelativePath,
            bool isNestedSubmodule)
        {
            var result = new List<PrInfo>();
            foreach (var submodule in Helpers.ParseSubmodules(repoPath, _pat))
            {
                var shaLine = await Helpers.RunGitCapture($"-C {repoPath} ls-tree {baseCommit} {submodule.Path}", _pat);
                var parts = shaLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                var sha = parts[2];
                var trimmed = Helpers.TrimSharedPrefix(submodule.Path);
                var newRelative = Path.Combine(sharedRelativePath, trimmed);
                var submodulePrs = await ProcessSubmodule(repoPath, submodule, sha, newRelative, isNestedSubmodule);
                result.AddRange(submodulePrs);
            }

            return result;
        }

        private async Task<List<PrInfo>> ProcessSubmodule(string repoPath, SubmoduleInfo submodule, string sha,
            string sharedRelativePath, bool isNestedSubmodule)
        {
            var path = Path.Combine(repoPath, submodule.Path);

            // Nested submodules are handled differently. Don't really know how it works. I've brute forced it.
            if (isNestedSubmodule)
            {
                if (Directory.Exists(path))
                    Helpers.DeleteDirectory(path);

                await Helpers.RunGit($"-C {repoPath} submodule update --init -- {submodule.Path}", _pat);
            }
            else
            {
                await Helpers.RunGit($"-C {repoPath} submodule add {submodule.Url} {submodule.Path}", _pat);
            }

            var exceptionCode = await Helpers.RunGit($"-C {path} checkout {sha}", _pat).SuppressGitException();
            if (exceptionCode != 0)
            {
                var message = $"WARNING! Failed to checkout submodule {submodule.Name} at {sha}. " +
                              "This is likely because the base branch is pointing to a squashed head. " +
                              "We'll use the default commit for the submodule i.e. main/HEAD." +
                              "Keep in mind that this is opening up a lot of potential issues so" +
                              " pay extra attention to this submodule when reviewing the PR.";

                Logger.Write(message);
                MessageBox.Show(message, "Submodule Checkout Warning");
            }

            if (_newBranches.TryGetValue(submodule.Name, out var newBranch))
                await Helpers.CreateRemoteBranchAtCommit(path, newBranch, sha, _pat);

            var nextPr = await GetNextPrNumber(submodule.Url);
            var branch = string.Format(Texts.TEMP_REWIRE_BRANCH_TEMPLATE, nextPr);
            await Helpers.CreateAndCheckoutBranch(path, branch, _pat, true);

            var shared = Path.Combine(
                Texts.RUNTIME_FOLDER,
                Texts.TEMP_FOLDER,
                Texts.SHARED_COPY_FOLDER,
                sharedRelativePath);
            
            Helpers.DeleteContentsExceptGit(path);
            
            Helpers.CopyDirectory(shared, path);

            // Note: Staging un unstaging clears the 'LF will be replaced by CRLF' warnings which cause fake diffs
            await Helpers.RunGit($"-C {path} add .", _pat);
            await Helpers.RunGit($"-C {path} reset", _pat);

            var nestedBase = await Helpers.GetMergeBase(path, _targetBranch, _pat);
            var nestedPrs = await ProcessSubmodules(path, nestedBase, sharedRelativePath, true);

            var diffExitCode = await Helpers.RunGit($"-C {path} diff --exit-code", _pat)
                .SuppressGitException();
            var untrackedOutput = await Helpers.RunGitCapture(
                $"-C {path} ls-files --others --exclude-standard",
                _pat);
            if (diffExitCode == 0 && string.IsNullOrEmpty(untrackedOutput) && nestedPrs.Count == 0)
                return new List<PrInfo>(nestedPrs);

            var (latestCommitMessage, latestCommitBody) = await GetLatestCommitMessage();
            await Helpers.RunGit($"-C {path} add .", _pat);
            await Helpers.RunGit($"-C {path} commit -m \"{latestCommitMessage}\" -m \"{latestCommitBody}\"", _pat);
            await Helpers.RunGit($"-C {path} push -u origin {branch}", _pat);

            var (prTitle, prBody) = await GetPreviousCommitMessage();
            var description = BuildPrDescription(prBody, nestedPrs);

            var (owner, name) = ParseOwnerAndName(submodule.Url);
            var client = CreateClient();
            var baseBranch = _newBranches.TryGetValue(submodule.Name, out var newBase)
                ? newBase
                : _baseRefs.GetValueOrDefault(submodule.Name, _targetBranch);
            var pr = await client.PullRequest.Create(owner, name,
                new NewPullRequest(prTitle, branch, baseBranch)
                {
                    Body = description
                });
            var result = new List<PrInfo>(nestedPrs)
            {
                new PrInfo(submodule.Name, pr.Number, pr.HtmlUrl, branch, submodule.Url)
            };

            return result;
        }

        private async Task CommitAndPush(string repoPath, string branch, int step, string details)
        {
            await Helpers.RunGit($"-C {repoPath} add .", _pat);
            var subject = string.Format(Texts.COMMIT_SUBJECT_TEMPLATE, step);
            await Helpers.RunGit($"-C {repoPath} commit -m \"{subject}\" -m \"{details}\"", _pat);
            await Helpers.RunGit($"-C {repoPath} push -u origin {branch}", _pat);
        }

        private async Task Commit(string repoPath, int step, string details)
        {
            await Helpers.RunGit($"-C {repoPath} add .", _pat);
            var subject = string.Format(Texts.COMMIT_SUBJECT_TEMPLATE, step);
            await Helpers.RunGit($"-C {repoPath} commit -m \"{subject}\" -m \"{details}\"", _pat);
        }

        private async Task<PullRequest> CreateMainPr(string repoUrl, string branch, List<PrInfo> submodulePrs)
        {
            var (prTitle, prBody) = await GetLatestCommitMessage();
            var description = BuildPrDescription(prBody, submodulePrs);

            var (owner, name) = ParseOwnerAndName(repoUrl);
            var client = CreateClient();
            return await client.PullRequest.Create(owner, name,
                new NewPullRequest(prTitle, branch, _targetBranch)
                {
                    Body = description
                });
        }

        private async Task AddMainPrLinkToSubmodules(string mainPrUrl, List<PrInfo> submodulePrs)
        {
            var client = CreateClient();
            foreach (var info in submodulePrs)
            {
                var (owner, name) = ParseOwnerAndName(info.RepoUrl);
                var pull = await client.PullRequest.Get(owner, name, info.Number);
                var newBody = pull.Body + "\n" + string.Format(Texts.MAIN_PR_LINK_TEMPLATE, mainPrUrl);
                await client.PullRequest.Update(owner, name, info.Number, new PullRequestUpdate { Body = newBody });
            }
        }

        private GitHubClient CreateClient()
        {
            var client = new GitHubClient(new ProductHeaderValue(Texts.GITHUB_CLIENT_PRODUCT));
            if (!string.IsNullOrEmpty(_pat))
                client.Credentials = new Credentials(_pat);
            return client;
        }

        private async Task<(string subject, string body)> GetLatestCommitMessage()
        {
            var output = await Helpers.RunGitCapture($"-C {_mainRepoPath} log -1 --pretty=%s%n%b {_sourceBranch}", _pat);
            var parts = output.Split(new[] { '\n' }, 2);
            var subject = parts[0];
            var body = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            return (subject, body);
        }

        private async Task<(string subject, string body)> GetPreviousCommitMessage()
        {
            var output = await Helpers.RunGitCapture($"-C {_mainRepoPath} log -1 --pretty=%s%n%b {_sourceBranch}^", _pat);
            var parts = output.Split(new[] { '\n' }, 2);
            var subject = parts[0];
            var body = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            return (subject, body);
        }

        private static string BuildPrDescription(string commitBody, List<PrInfo> submodulePrs)
        {
            var description = Texts.REWIRING_FOR_DEV_ENVIRONMENT + "\n" +
                              Texts.PR_DESCRIPTION;

            if (submodulePrs.Count > 0)
            {
                description += Texts.PR_SUBMODULE_CHANGES;

                foreach (var pr in submodulePrs)
                    description += $"- {pr.Name}: {pr.Url}\n";

                description += Texts.PR_SUBMODULE_WARNING;
            }

            return $"{description}\n{commitBody}";
        }

        public async Task<List<SubmoduleInfo>> GetChangedSubmodules()
        {
            await PrepareMainRepo(true);

            var mergeBase = await Helpers.GetMergeBase(_mainRepoPath, _targetBranch, _pat);

            var result = await CollectChangedSubmodules(_mainRepoPath, mergeBase, string.Empty, false);

            Helpers.DeleteDirectory(_mainRepoPath);

            return result;
        }

        private async Task<List<SubmoduleInfo>> CollectChangedSubmodules(
            string repoPath,
            string baseCommit,
            string sharedRelativePath,
            bool isNestedSubmodule)
        {
            var result = new List<SubmoduleInfo>();
            foreach (var submodule in Helpers.ParseSubmodules(repoPath, _pat))
            {
                var shaLine = await Helpers.RunGitCapture($"-C {repoPath} ls-tree {baseCommit} {submodule.Path}", _pat);
                var parts = shaLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                var sha = parts[2];
                var trimmed = Helpers.TrimSharedPrefix(submodule.Path);
                var newRelative = Path.Combine(sharedRelativePath, trimmed);
                var changed = await CollectChangedSubmodule(repoPath, submodule, sha, newRelative, isNestedSubmodule);
                result.AddRange(changed);
            }

            return result;
        }

        private async Task<List<SubmoduleInfo>> CollectChangedSubmodule(
            string repoPath,
            SubmoduleInfo submodule,
            string sha,
            string sharedRelativePath,
            bool isNestedSubmodule)
        {
            var path = Path.Combine(repoPath, submodule.Path);

            if (isNestedSubmodule)
            {
                if (Directory.Exists(path))
                    Helpers.DeleteDirectory(path);

                await Helpers.RunGit($"-C {repoPath} submodule update --init -- {submodule.Path}", _pat);
            }
            else
            {
                await Helpers.RunGit($"-C {repoPath} submodule add {submodule.Url} {submodule.Path}", _pat);
            }

            var exceptionCode = await Helpers.RunGit($"-C {path} checkout {sha}", _pat).SuppressGitException();
            if (exceptionCode != 0)
            {
                var message = $"WARNING! Failed to checkout submodule {submodule.Name} at {sha}. " +
                              "This is likely because the base branch is pointing to a squashed head. " +
                              "We'll use the default commit for the submodule i.e. main/HEAD." +
                              "Keep in mind that this is opening up a lot of potential issues so" +
                              " pay extra attention to this submodule when reviewing the PR.";

                Logger.Write(message);
            }

            var branch = string.Format(Texts.TEMP_REWIRE_BRANCH_TEMPLATE, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await Helpers.CreateAndCheckoutBranch(path, branch, _pat, true);

            var shared = Path.Combine(
                Texts.RUNTIME_FOLDER,
                Texts.TEMP_FOLDER,
                Texts.SHARED_COPY_FOLDER,
                sharedRelativePath);

            Helpers.DeleteContentsExceptGit(path);

            Helpers.CopyDirectory(shared, path);

            await Helpers.RunGit($"-C {path} add .", _pat);
            await Helpers.RunGit($"-C {path} reset", _pat);

            var nestedBase = await Helpers.GetMergeBase(path, _targetBranch, _pat);
            var nestedChanges = await CollectChangedSubmodules(path, nestedBase, sharedRelativePath, true);

            var diffExitCode = await Helpers.RunGit($"-C {path} diff --exit-code", _pat)
                .SuppressGitException();
            var untrackedOutput = await Helpers.RunGitCapture(
                $"-C {path} ls-files --others --exclude-standard",
                _pat);
            if (diffExitCode == 0 && string.IsNullOrEmpty(untrackedOutput) && nestedChanges.Count == 0)
                return new List<SubmoduleInfo>(nestedChanges);

            var result = new List<SubmoduleInfo>(nestedChanges)
            {
                submodule
            };

            return result;
        }


        private record PrInfo(string Name, int Number, string Url, string Branch, string RepoUrl);
    }
}

