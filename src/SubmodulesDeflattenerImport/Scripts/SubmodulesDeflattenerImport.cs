using System.Threading.Tasks;
using Common.Scripts;

namespace SubmodulesDeflattenerImport.Scripts
{
    internal class SubmodulesDeflattenerImport
    {
        private readonly string? _pat;
        private readonly string _repoUrl;
        private readonly string _mainRepoPath;
        private readonly string _targetBranch;
        private readonly string _sourceBranch;

        public SubmodulesDeflattenerImport(string? pat, string repoUrl,
            string targetBranch, string sourceBranch)
        {
            _pat = pat;
            _repoUrl = repoUrl;
            _targetBranch = targetBranch;
            _sourceBranch = sourceBranch;

            var runtime = PrepareRuntime();
            Logger.Write(string.Format(Texts.RUNTIME_FOLDER_READY, runtime));

            var repoName = RepoUtils.GetRepoName(repoUrl);
            Logger.Write(string.Format(Texts.REPOSITORY_NAME_RESOLVED, repoName));

            _mainRepoPath = Path.Combine(runtime, repoName);
        }

        public async Task Run()
        {
            await CloneRepository(_repoUrl, _mainRepoPath);

            await CheckoutBranch(_mainRepoPath, _sourceBranch);

            BackupFiles(_mainRepoPath);

            await CheckoutBranch(_mainRepoPath, _targetBranch);

            var branch = string.Format(Texts.TEMP_REWIRE_BRANCH_TEMPLATE,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await Helpers.CreateAndCheckoutBranch(_mainRepoPath, branch, _pat, true);

            RemoveUnnecessaryFiles(_mainRepoPath);
            MoveSharedFolder(_mainRepoPath);
            DeleteGitFiles(_mainRepoPath);
            RestoreGitFiles(_mainRepoPath);
            await Commit(_mainRepoPath, 1, Texts.REMOVE_CODEX_FILES_DETAILS);

            var mergeBase = await Helpers.GetMergeBase(_mainRepoPath, _sourceBranch, _pat);

            await ProcessSubmodules(_mainRepoPath, mergeBase, string.Empty, false);

            Helpers.SanitizeGitModules(_mainRepoPath, _pat);

            RestoreSharedMetaFiles(_mainRepoPath);
            await Commit(_mainRepoPath, 2, Texts.UPDATE_SUBMODULES_DETAILS);

            if (await MergeMainBranch() == false)
                return;

            await FlattenRepository(_mainRepoPath);

            await CheckoutBranch(_mainRepoPath, _targetBranch);
            var mergeExitCode = await SquashMergeIntoTarget(branch);
            if (mergeExitCode == 0)
                await Helpers.DeleteBranchIfExists(_mainRepoPath, branch, _pat);
            else
                Logger.Write(Texts.MERGE_FAILED_ERROR);
        }

        private static string PrepareRuntime()
        {
            Logger.Write(Texts.PREPARING_RUNTIME_FOLDER);
            if (Directory.Exists(Texts.RUNTIME_FOLDER))
                Helpers.DeleteDirectory(Texts.RUNTIME_FOLDER);

            Directory.CreateDirectory(Texts.RUNTIME_FOLDER);
            var path = Path.GetFullPath(Texts.RUNTIME_FOLDER);
            Logger.Write(string.Format(Texts.RUNTIME_FOLDER_READY, path));
            return path;
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





        private async Task ProcessSubmodules(
            string repoPath,
            string baseCommit,
            string sharedRelativePath,
            bool isNestedSubmodule)
        {
            foreach (var submodule in Helpers.ParseSubmodules(repoPath, _pat))
            {
                var shaLine = await Helpers.RunGitCapture($"-C {repoPath} ls-tree {baseCommit} {submodule.Path}", _pat);
                var parts = shaLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                var sha = parts[2];
                var trimmed = Helpers.TrimSharedPrefix(submodule.Path);
                var newRelative = Path.Combine(sharedRelativePath, trimmed);
                await ProcessSubmodule(repoPath, submodule, sha, newRelative, isNestedSubmodule);
            }
        }

        private async Task ProcessSubmodule(string repoPath, SubmoduleInfo submodule, string sha,
            string sharedRelativePath, bool isNestedSubmodule)
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

            await Helpers.RunGit($"-C {path} checkout {sha}", _pat);

            var branch = string.Format(Texts.TEMP_REWIRE_BRANCH_TEMPLATE,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await Helpers.CreateAndCheckoutBranch(path, branch, _pat, true);

            var shared = Path.Combine(
                Texts.RUNTIME_FOLDER,
                Texts.TEMP_FOLDER,
                Texts.SHARED_COPY_FOLDER,
                sharedRelativePath);
            Helpers.CopyDirectory(shared, path);

            await Helpers.RunGit($"-C {path} add .", _pat);
            await Helpers.RunGit($"-C {path} reset", _pat);

            var nestedBase = await Helpers.GetMergeBase(path, _sourceBranch, _pat);
            await ProcessSubmodules(path, nestedBase, sharedRelativePath, true);

            var diffExitCode = await Helpers.RunGit($"-C {path} diff --exit-code", _pat)
                .SuppressGitException();
            if (diffExitCode == 0)
                return;

            await Helpers.RunGit($"-C {path} add .", _pat);
            await Helpers.RunGit($"-C {path} commit -m \"{Texts.SQUASH_COMMIT_MESSAGE}\"", _pat);
        }

        private async Task Commit(string repoPath, int step, string details)
        {
            await Helpers.RunGit($"-C {repoPath} add .", _pat);
            var subject = string.Format(Texts.COMMIT_SUBJECT_TEMPLATE, step);
            await Helpers.RunGit($"-C {repoPath} commit -m \"{subject}\" -m \"{details}\"", _pat);
        }

        private async Task<bool> MergeMainBranch()
        {
            var exitCode = await Helpers.RunGit($"-C {_mainRepoPath} merge {_sourceBranch}", _pat);
            if (exitCode == 0)
                return true;

            if (await TryFixSubmoduleConflicts() == false)
            {
                Logger.Write("Must first manually resolve the conflicts");
                return false;
            }

            await Helpers.RunGit($"-C {_mainRepoPath} add -A", _pat);
            await Helpers.RunGit($"-C {_mainRepoPath} commit -m \"Merge {_sourceBranch}\"", _pat);
            return true;
        }

        private async Task<bool> TryFixSubmoduleConflicts()
        {
            var output = await Helpers.RunGitCapture($"-C {_mainRepoPath} diff --name-only --diff-filter=U", _pat);
            if (string.IsNullOrEmpty(output))
                return true;

            var conflicts = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var conflict in conflicts)
            {
                var submodulePath = Path.Combine(_mainRepoPath, conflict);
                var gitDir = Path.Combine(submodulePath, ".git");
                var hasGitDir = Directory.Exists(gitDir) || File.Exists(gitDir);
                if (hasGitDir == false)
                    return false;

                var shaLine = await Helpers.RunGitCapture($"-C {_mainRepoPath} ls-tree {_sourceBranch} {conflict}", _pat);
                var parts = shaLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    return false;

                var sha = parts[2];
                await Helpers.RunGit($"-C {submodulePath} merge {sha}", _pat);
                await Helpers.RunGit($"-C {submodulePath} add -A", _pat);
                await Helpers.RunGit($"-C {submodulePath} commit -m \"Merge {sha}\"", _pat);
                await Helpers.RunGit($"-C {_mainRepoPath} add {conflict}", _pat);
            }

            output = await Helpers.RunGitCapture($"-C {_mainRepoPath} diff --name-only --diff-filter=U", _pat);
            return string.IsNullOrEmpty(output);
        }

        private async Task<int> SquashMergeIntoTarget(string branch)
        {
            var exitCode = await Helpers.RunGit($"-C {_mainRepoPath} merge --squash {branch}", _pat);
            if (exitCode == 0)
                await Helpers.RunGit($"-C {_mainRepoPath} commit -m \"{Texts.SQUASH_MERGE_COMMIT_MESSAGE}\"", _pat);

            return exitCode;
        }

        private async Task FlattenRepository(string repoPath)
        {
            HandleGitignoreFields(repoPath);
            await Flatten(repoPath);
            await Helpers.RunGit($"-C {repoPath} add -A", _pat);
            await Helpers.RunGit($"-C {repoPath} commit -m \"Flatten submodules\"", _pat);
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
                    Helpers.DeleteFileWithRetry(submoduleGit);

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


    }
}
