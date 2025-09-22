using System;
using System.Diagnostics;
using Common.Scripts;
using System.Threading.Tasks;

namespace Common.Scripts;

public class CsprojRegen
{
    readonly string _unityExe;
    readonly string _repoPath;

    public CsprojRegen(string unityExe, string repoPath)
    {
        _unityExe = ResolveUnityExecutablePath(unityExe);
        _repoPath = Path.TrimEndingDirectorySeparator(repoPath);
    }

    public async Task Run(bool makeCommits = true, bool pushWhenDone = true)
    {
        var editorFolder = Path.Combine(_repoPath, "Assets", "Editor");
        var csPath = Path.Combine(editorFolder, "RegenerateProjectFiles.cs");
        var isFolderCreated = false;
        
        if (Directory.Exists(editorFolder) == false)
        {
            Directory.CreateDirectory(editorFolder);
            isFolderCreated = true;
        }
        
        var sourcePath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Context",
                "RegenerateProjectFiles.cs.txt"));
        File.Copy(sourcePath, csPath, true);

        var args =
            $"-batchmode -quit -projectPath \"{_repoPath}\" " +
            "-executeMethod RegenerateProjectFiles.Regenerate";
        await RunUnity(args);

        var csMetaPath = csPath + ".meta";
        if (File.Exists(csMetaPath))
            File.Delete(csMetaPath);

        if (isFolderCreated)
        {
            Directory.Delete(editorFolder, true);
            var editorMetaPath = editorFolder + ".meta";
            if (File.Exists(editorMetaPath))
                File.Delete(editorMetaPath);
        }
        else
            File.Delete(csPath);

        if (makeCommits)
        {
            await Helpers.RunGit($"-C {_repoPath} add -A");
            await Helpers.RunGit($"-C {_repoPath} commit -m \"Csproj regenerated\"");
            if (pushWhenDone)
                await Helpers.RunGit($"-C {_repoPath} push --set-upstream origin HEAD");
        }
    }

    async Task RunUnity(string arguments)
    {
        var commandLabel = string.Format("{0} {1}", _unityExe, arguments).Trim();
        Logger.LogInfo(commandLabel, string.Format(Texts.RUNNING_COMMAND, _unityExe, arguments));
        var stopwatch = Stopwatch.StartNew();
        var (exitCode, output, error) = await RunUnityProcess(arguments);

        if (exitCode == Texts.UNITY_ALREADY_OPEN_EXIT_CODE)
        {
            Logger.LogWarning(commandLabel, Texts.KILLING_UNITY_PROCESS);
            KillUnityProcesses();
            (exitCode, output, error) = await RunUnityProcess(arguments);
        }

        stopwatch.Stop();

        var consoleOut = exitCode != 0
            ? string.IsNullOrEmpty(error) ? output : error
            : output;
        var message = string.Format(Texts.COMMAND_EXITED_TEMPLATE, exitCode, arguments);
        if (string.IsNullOrEmpty(consoleOut) == false)
            message += string.Format(Texts.OUTPUT_TEMPLATE, consoleOut);

        var level = exitCode == 0 ? LogLevel.Success : LogLevel.Error;
        Logger.LogOperationResult(commandLabel, message, stopwatch.Elapsed, level);

        if (exitCode != 0)
            throw new InvalidOperationException(message);
    }

    async Task<(int exitCode, string output, string error)> RunUnityProcess(string arguments)
    {
        var psi = new ProcessStartInfo(_unityExe, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException(Texts.FAILED_TO_START_PROCESS);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        var output = await stdoutTask;
        var error = await stderrTask;
        return (process.ExitCode, output, error);
    }

    static void KillUnityProcesses()
    {
        foreach (var process in Process.GetProcessesByName("Unity"))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // ignored
            }
        }
    }

    static string ResolveUnityExecutablePath(string unityPath)
    {
        if (string.IsNullOrWhiteSpace(unityPath))
            throw new ArgumentException("Unity path cannot be empty.", nameof(unityPath));

        var trimmedPath = Path.TrimEndingDirectorySeparator(unityPath);

        if (OperatingSystem.IsMacOS() &&
            trimmedPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            var macExecutablePath = Path.Combine(trimmedPath, "Contents", "MacOS", "Unity");
            if (File.Exists(macExecutablePath))
                return macExecutablePath;
        }

        return trimmedPath;
    }

}
