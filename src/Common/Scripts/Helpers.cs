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

        public static Task<string> GetMergeBase(string repoPath, string branch, string? pat = null) =>
            RunGitCapture(string.Format(Texts.MERGE_BASE_BRANCH_HEAD, repoPath, branch), pat);

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
            if (Directory.Exists(Texts.RUNTIME_FOLDER))
                DeleteDirectory(Texts.RUNTIME_FOLDER);

            Directory.CreateDirectory(Texts.RUNTIME_FOLDER);
            var path = Path.GetFullPath(Texts.RUNTIME_FOLDER);
            Logger.Write(string.Format(Texts.RUNTIME_FOLDER_READY, path));
            return path;
        }
    }
}
