using System;
using System.Diagnostics;

namespace Common.Scripts
{
    public static class Helpers
    {
        public static async Task<int> RunGit(string args, string? pat = null)
        {
            var (exitCode, message) = await RunGit(args, (code, _, _) => code, pat);
            if (exitCode != 0)
            {
                Logger.Write(message);
                throw new GitException(message, exitCode);
            }

            return exitCode;
        }

        public static async Task<string> RunGitCapture(string args, string? pat = null)
        {
            var (output, _) = await RunGit(args, (_, output, _) => output, pat);
            return output;
        }

        public static Task<(T result, string message)> RunGit<T>(string args, Func<int, string, string, T> selector, string? pat = null) =>
            RunProcess(Texts.GIT, args, selector, pat);

        static async Task<(T result, string message)> RunProcess<T>(string fileName, string arguments, Func<int, string, string, T> selector, string? pat)
        {
            var commandLabel = string.Format("{0} {1}", fileName, arguments).Trim();
            Logger.LogInfo(commandLabel, string.Format(Texts.RUNNING_COMMAND, fileName, arguments));
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (string.IsNullOrEmpty(pat) == false)
                psi.Environment[Texts.GITHUB_TOKEN_ENV] = pat;

            var stopwatch = Stopwatch.StartNew();

            using var process = Process.Start(psi)
                              ?? throw new InvalidOperationException(Texts.FAILED_TO_START_PROCESS);

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await stdoutTask;
            var error = await stderrTask;

            stopwatch.Stop();

            var writeMessage = string.Format(Texts.COMMAND_EXITED_TEMPLATE, process.ExitCode, arguments);
            var consoleOut = process.ExitCode != 0 ? error : output;
            if (string.IsNullOrEmpty(consoleOut) == false)
                writeMessage += string.Format(Texts.OUTPUT_TEMPLATE, consoleOut);

            var level = process.ExitCode == 0 ? LogLevel.Success : LogLevel.Error;
            Logger.LogOperationResult(commandLabel, writeMessage, stopwatch.Elapsed, level);

            var result = selector(process.ExitCode, output.Trim(), error.Trim());
            return (result, writeMessage);
        }

        public static void SanitizeGitModules(string repoPath, string? pat = null)
        {
            var modulesPath = Path.Combine(repoPath, Texts.DOT_GITMODULES);
            if (File.Exists(modulesPath) == false)
                return;

            RemovePatTokens(modulesPath, pat);
        }

        [Conditional("RELEASE")]
        public static void InsertPatToken(ref string url, string? pat)
        {
            if (string.IsNullOrEmpty(pat) == false)
                url = url.Replace("https://", $"https://x-access-token:{pat}@");
        }

        [Conditional("RELEASE")]
        static void RemovePatTokens(string modulesPath, string? pat)
        {
            if (string.IsNullOrEmpty(pat))
                return;

            var content = File.ReadAllText(modulesPath);
            content = content.Replace($"https://x-access-token:{pat}@", "https://");
            File.WriteAllText(modulesPath, content);

            var repoPath = Path.GetDirectoryName(modulesPath)!;
            foreach (var submodule in ParseSubmodules(repoPath, pat))
            {
                var submodulePath = Path.Combine(repoPath, submodule.Path);
                SanitizeGitModules(submodulePath, pat);
            }
        }

        public static IEnumerable<SubmoduleInfo> ParseSubmodules(string repoPath, string? pat = null)
        {
            var modulesPath = Path.Combine(repoPath, Texts.DOT_GITMODULES);
            if (File.Exists(modulesPath) == false)
                return Array.Empty<SubmoduleInfo>();

            var result = new List<SubmoduleInfo>();
            string? name = null;
            string? path = null;
            string? url = null;

            foreach (var line in File.ReadLines(modulesPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[submodule"))
                {
                    if (name != null && path != null && url != null)
                        result.Add(new SubmoduleInfo(name, path, url));

                    var start = trimmed.IndexOf('"') + 1;
                    var end = trimmed.LastIndexOf('"');
                    name = trimmed[start..end];
                    path = null;
                    url = null;
                }
                else if (trimmed.StartsWith("path ="))
                {
                    path = trimmed.Substring(6).Trim();
                }
                else if (trimmed.StartsWith("url ="))
                {
                    url = trimmed.Substring(5).Trim();
                    InsertPatToken(ref url, pat);
                }
            }

            if (name != null && path != null && url != null)
                result.Add(new SubmoduleInfo(name, path, url));

            return result;
        }

        public static async Task<(string shaBase, bool foundBase)> GetMergeBase(
            string repoPath,
            string branch,
            string? pat = null)
        {
            var command = string.Format(Texts.MERGE_BASE_BRANCH_HEAD, repoPath, branch);

            for (var attempt = 0; attempt < Texts.MERGE_BASE_FETCH_ATTEMPTS; attempt++)
            {
                string mergeBase;
                try
                {
                    mergeBase = await RunGitCapture(command, pat);
                }
                catch (GitException ex) when (IsMergeBaseUnavailable(ex))
                {
                    return (string.Empty, false);
                }
                catch (GitException)
                {
                    if (attempt == Texts.MERGE_BASE_FETCH_ATTEMPTS - 1)
                        throw;

                    if (await TryFetchMergeBaseHistory(repoPath, branch, pat, attempt) == false)
                        return (string.Empty, false);

                    continue;
                }

                if (string.IsNullOrWhiteSpace(mergeBase) == false)
                    return (mergeBase, true);

                if (attempt == Texts.MERGE_BASE_FETCH_ATTEMPTS - 1)
                    break;

                if (await TryFetchMergeBaseHistory(repoPath, branch, pat, attempt) == false)
                    return (string.Empty, false);
            }

            try
            {
                var mergeBase = await RunGitCapture(command, pat);
                return (mergeBase, string.IsNullOrWhiteSpace(mergeBase) == false);
            }
            catch (GitException ex) when (IsMergeBaseUnavailable(ex))
            {
                return (string.Empty, false);
            }
        }

        static async Task<bool> TryFetchMergeBaseHistory(string repoPath, string branch, string? pat, int attempt)
        {
            try
            {
                var deepen = Texts.MERGE_BASE_DEEPEN_INCREMENT << attempt;
                var remote = await GetRemoteOrDefault(repoPath, branch, pat);
                await RunGit($"-C {repoPath} fetch --deepen {deepen} {remote} {branch}", pat);

                var headBranch = await GetCurrentBranch(repoPath, pat);
                if (headBranch is string current && string.IsNullOrWhiteSpace(current) == false)
                {
                    var headRemote = await TryGetBranchRemote(repoPath, current, pat);
                    if (string.IsNullOrWhiteSpace(headRemote) == false)
                        await RunGit($"-C {repoPath} fetch --deepen {deepen} {headRemote} {current}", pat);
                }
            }
            catch (GitException ex) when (IsMergeBaseUnavailable(ex))
            {
                return false;
            }

            return true;
        }

        static bool IsMergeBaseUnavailable(GitException exception)
        {
            var message = exception.Message;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("couldn't find remote ref", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("unknown revision or path not in the working tree", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("not a valid object name", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("needed a single revision", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static async Task<string> GetRemoteOrDefault(string repoPath, string branch, string? pat)
        {
            var remote = await TryGetBranchRemote(repoPath, branch, pat);
            if (string.IsNullOrWhiteSpace(remote))
                return Texts.ORIGIN_REMOTE;

            return remote;
        }

        static async Task<string?> GetCurrentBranch(string repoPath, string? pat)
        {
            try
            {
                var branch = await RunGitCapture($"-C {repoPath} rev-parse --abbrev-ref HEAD", pat);
                return branch == "HEAD" ? null : branch;
            }
            catch (GitException)
            {
                return null;
            }
        }

        static async Task<string?> TryGetBranchRemote(string repoPath, string branch, string? pat)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return null;

            try
            {
                var remote = await RunGitCapture($"-C {repoPath} config --get branch.{branch}.remote", pat);
                return string.IsNullOrWhiteSpace(remote) ? null : remote;
            }
            catch (GitException)
            {
                return null;
            }
        }

        public static async Task EnsureBranchAvailability(string repoPath, string branch, string? pat = null)
        {
            var branchList = await RunGitCapture($"-C {repoPath} branch --list {branch}", pat);
            if (string.IsNullOrWhiteSpace(branchList) == false)
                return;

            var remote = await TryGetBranchRemote(repoPath, branch, pat) ?? Texts.ORIGIN_REMOTE;

            Logger.Write(string.Format(Texts.FETCHING_BRANCH_STATUS, branch, remote));
            await RunGit($"-C {repoPath} fetch --depth 1 {remote} {branch}:{branch}", pat);
        }

        public static async Task<List<string>> GetRemoteBranches(string repo, string? pat = null)
        {
            var output = await RunGitCapture(string.Format(Texts.LIST_REMOTE_BRANCHES, repo), pat);
            var result = new List<string>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length > 1 && parts[1].StartsWith(Texts.REFS_HEADS_PREFIX))
                    result.Add(parts[1][Texts.REFS_HEADS_PREFIX.Length..]);
            }
            return result;
        }

        public static async Task DeleteBranchIfExists(string repoPath, string branch, string? pat = null)
        {
            var output = await RunGitCapture($"-C {repoPath} branch --list {branch}", pat);
            if (string.IsNullOrEmpty(output) == false)
                await RunGit($"-C {repoPath} branch -D {branch}", pat);

            var remoteOutput = await RunGitCapture($"-C {repoPath} branch -a --list *{branch}", pat);
            if (string.IsNullOrEmpty(remoteOutput) == false && remoteOutput.Contains("remotes/origin"))
                await RunGit($"-C {repoPath} push origin --delete {branch}", pat);
        }

        public static async Task DeleteRemoteBranchIfExists(string repoUrl, string branch, string? pat = null)
        {
            var branches = await GetRemoteBranches(repoUrl, pat);
            if (branches.Contains(branch))
                await RunGit($"push {repoUrl} --delete {branch}", pat);
        }

        public static async Task CreateAndCheckoutBranch(string repoPath, string branch, string? pat = null,
            bool deleteIfExists = false)
        {
            if (deleteIfExists)
                await DeleteBranchIfExists(repoPath, branch, pat);

            Logger.Write(string.Format(Texts.CREATING_AND_CHECKING_OUT, branch));
            await RunGit(string.Format(Texts.CHECKING_OUT, repoPath, $"-b {branch}"), pat);
        }

        public static async Task CreateRemoteBranchAtCommit(string repoPath, string branch, string sha, string? pat = null)
        {
            await RunGit($"-C {repoPath} branch {branch} {sha}", pat);
            await RunGit($"-C {repoPath} push -u origin {branch}", pat);
        }

        public static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path) == false)
                return;

            var dir = new DirectoryInfo(path);
            foreach (var info in dir.GetFileSystemInfos("*", SearchOption.AllDirectories))
                info.Attributes = FileAttributes.Normal;

            dir.Attributes = FileAttributes.Normal;
            Directory.Delete(path, true);
        }

        public static void DeleteContentsExceptGit(string path)
        {
            if (Directory.Exists(path) == false)
                return;

            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                if (Path.GetFileName(entry).Contains(".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Directory.Exists(entry))
                    DeleteDirectory(entry);
                else
                    DeleteFileWithRetry(entry);
            }
        }

        public static void DeleteFileWithRetry(string path, int retries = 3, int delayMs = 100)
        {
            if (File.Exists(path) == false)
                return;

            for (var i = 0; i < retries; i++)
            {
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return;
                }
                catch (IOException) when (i < retries - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        public static void CopyDirectory(string source, string dest)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }

        public static void DeleteGitIgnore(string path)
        {
            var gitIgnore = Path.Combine(path, ".gitignore");
            if (File.Exists(gitIgnore))
                File.Delete(gitIgnore);
        }

        public static string TrimSharedPrefix(string path)
        {
            var prefix = Path.Combine(Texts.ASSETS_FOLDER, Texts.SHARED_FOLDER) + Path.DirectorySeparatorChar;
            var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? normalized[prefix.Length..]
                : normalized;
        }
        
        public static string PrepareRuntime()
        {
            Logger.Write(Texts.PREPARING_RUNTIME_FOLDER);
            Directory.CreateDirectory(Texts.RUNTIME_FOLDER);
            var path = Path.GetFullPath(Texts.RUNTIME_FOLDER);

            var folders = new[]
            {
                Path.Combine(path, Texts.SETUP_FOLDER),
                Path.Combine(path, Texts.EXPORT_FOLDER)
            };

            foreach (var folder in folders)
                Directory.CreateDirectory(folder);

            Logger.Write(string.Format(Texts.RUNTIME_FOLDER_READY, path));
            return path;
        }

        public static string GetRuntimeRepositoryPath(string scope, string repoUrl)
        {
            var runtime = PrepareRuntime();
            var basePath = Path.Combine(runtime, scope);
            Directory.CreateDirectory(basePath);
            var repoName = RepoUtils.GetRepoName(repoUrl);
            return Path.Combine(basePath, repoName);
        }

        public static string PrepareTempFolder()
        {
            var runtime = PrepareRuntime();
            var temp = Path.Combine(runtime, Texts.TEMP_FOLDER);
            if (Directory.Exists(temp))
                DeleteDirectory(temp);

            Directory.CreateDirectory(temp);
            return temp;
        }

        public static string GetTempFolderPath()
        {
            var runtime = Path.GetFullPath(Texts.RUNTIME_FOLDER);
            var temp = Path.Combine(runtime, Texts.TEMP_FOLDER);
            if (Directory.Exists(temp) == false)
                Directory.CreateDirectory(temp);

            return temp;
        }

        public static async Task EnsureRemote(string repoPath, string remoteName, string url, string? pat = null)
        {
            var remotes = await RunGitCapture($"-C {repoPath} remote", pat);
            var remoteExists = false;
            foreach (var remote in remotes.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(remote, remoteName, StringComparison.OrdinalIgnoreCase))
                {
                    remoteExists = true;
                    break;
                }

            if (remoteExists)
            {
                var existingUrl = await RunGitCapture($"-C {repoPath} remote get-url {remoteName}", pat);
                if (AreRepositoryUrlsEqual(existingUrl, url) == false)
                    await RunGit($"-C {repoPath} remote set-url {remoteName} {url}", pat);
            }
            else
            {
                await RunGit($"-C {repoPath} remote add {remoteName} {url}", pat);
            }
        }

        public static async Task ConfigureBranchRemote(string repoPath, string branch, string remoteName, string? pat = null)
        {
            await RunGit($"-C {repoPath} config branch.{branch}.remote {remoteName}", pat);
            await RunGit($"-C {repoPath} config branch.{branch}.merge refs/heads/{branch}", pat);
        }

        public static async Task PrepareRepositoryCache(string url, string path, string? pat = null)
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent) == false)
                Directory.CreateDirectory(parent);

            var shouldClone = true;
            var gitDirectory = Path.Combine(path, ".git");

            if (Directory.Exists(path) && Directory.Exists(gitDirectory))
            {
                var remoteUrl = await RunGitCapture($"-C {path} remote get-url origin", pat);
                if (AreRepositoryUrlsEqual(remoteUrl, url))
                {
                    Logger.Write(string.Format(Texts.RESETTING_CACHED_REPOSITORY, path));
                    await RunGit($"-C {path} remote set-url origin {url}", pat);
                    await RunGit($"-C {path} reset --hard", pat);
                    await RunGit($"-C {path} clean -xfd", pat);
                    await RunGit($"-C {path} fetch --depth 1 --force origin", pat);
                    Logger.Write(string.Format(Texts.REUSING_CACHED_REPOSITORY, path));
                    shouldClone = false;
                }
                else
                {
                    Logger.Write(string.Format(Texts.CLEARING_CACHED_REPOSITORY, path));
                    DeleteDirectory(path);
                }
            }
            else if (Directory.Exists(path))
            {
                Logger.Write(string.Format(Texts.CLEARING_CACHED_REPOSITORY, path));
                DeleteDirectory(path);
            }

            if (shouldClone)
            {
                Logger.Write(string.Format(Texts.CLONING_REPOSITORY, url, path));
                await RunGit(string.Format(Texts.CLONE_COMMAND, url, path), pat);
            }
        }

        static bool AreRepositoryUrlsEqual(string first, string second)
        {
            var normalizedFirst = NormalizeRepositoryUrl(first);
            var normalizedSecond = NormalizeRepositoryUrl(second);
            return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
        }

        static string NormalizeRepositoryUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            var trimmed = url.Trim();

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath.Trim('/');
                if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    path = path[..^4];

                return string.Format("{0}/{1}", uri.Host, path).ToLowerInvariant();
            }

            var sshIndex = trimmed.IndexOf(':');
            if (sshIndex >= 0)
            {
                var path = trimmed[(sshIndex + 1)..].Trim('/');
                if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    path = path[..^4];

                return path.ToLowerInvariant();
            }

            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^4];

            return trimmed.Trim('/').ToLowerInvariant();
        }
    }
}
