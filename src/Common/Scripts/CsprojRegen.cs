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
        _unityExe = unityExe;
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
        RunUnity(args);

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

    void RunUnity(string arguments)
    {
        Logger.Write(string.Format(Texts.RUNNING_COMMAND, _unityExe, arguments));
        var (exitCode, output, error) = RunUnityProcess(arguments);

        if (exitCode == Texts.UNITY_ALREADY_OPEN_EXIT_CODE)
        {
            Logger.Write(Texts.KILLING_UNITY_PROCESS);
            KillUnityProcesses();
            (exitCode, output, error) = RunUnityProcess(arguments);
        }

        var consoleOut = exitCode != 0
            ? string.IsNullOrEmpty(error) ? output : error
            : output;
        var message = string.Format(Texts.COMMAND_EXITED_TEMPLATE, exitCode, arguments);
        if (string.IsNullOrEmpty(consoleOut) == false)
            message += string.Format(Texts.OUTPUT_TEMPLATE, consoleOut);
        Logger.Write(message);

        if (exitCode != 0)
            throw new InvalidOperationException(message);
    }

    (int exitCode, string output, string error) RunUnityProcess(string arguments)
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
        process.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
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

}
