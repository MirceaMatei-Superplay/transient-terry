using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Common.Scripts;
using ExportTool = SubmodulesDeflattenerExport.Scripts.SubmodulesDeflattenerExport;
using SetupRunner = SubmodulesFlattenerSetup.Scripts.SubmodulesFlattenerSetup;
using CsprojApp = CsprojSetupToolApp.Apps.CsprojSetupToolApp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    async Task ExecuteOperation(string title, Func<Task> operation)
    {
        Logger.LogInfo(title, string.Format(CultureInfo.InvariantCulture, "{0} started", title));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await operation();
            stopwatch.Stop();
            Logger.LogOperationResult(title,
                string.Format(CultureInfo.InvariantCulture, "{0} completed successfully", title),
                stopwatch.Elapsed,
                LogLevel.Success);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var message = string.Format(CultureInfo.InvariantCulture,
                "{0} failed: {1}",
                title,
                exception.Message);
            Logger.LogOperationResult(title, message, stopwatch.Elapsed, LogLevel.Error);
            await ShowMessage(message,
                "Error",
                "!",
                "#FF6B6B",
                "#2C1B1B",
                exception.ToString());
        }
    }

    async Task RunSetup()
    {
        SaveSettings();
        var runner = new SetupRunner(_settings.UnityPath,
            _settings.SetupSourceRepoUrl,
            _settings.SetupTargetRepoUrl,
            _settings.SetupSourceBranch,
            _settings.SetupTargetBranch,
            _settings.Pat);
        await runner.Run();
        await ShowMessage(
            "You're all set! The setup finished successfully and your repository is ready to go.",
            "Setup Complete",
            "✔");
    }

    async Task RunExport()
    {
        SaveSettings();
        var baseRefs = new Dictionary<string, string>();
        foreach (var info in _diffSubmodules)
            if (_submoduleBranchBoxes.TryGetValue(info.Name, out var box) && box.SelectedItem != null)
                baseRefs[info.Name] = box.SelectedItem.ToString()!;

        var newBranches = new Dictionary<string, string>();
        foreach (var (name, check) in _submoduleMakeBranchChecks)
            if (check.IsChecked == true)
                newBranches[name] = _submoduleNewBranchBoxes[name]!.Text ?? string.Empty;

        var exporter = new ExportTool(_settings.Pat,
            _settings.ExportSourceRepoUrl, _settings.ExportTargetRepoUrl,
            _settings.ExportSourceBranch, _settings.ExportTargetBranch, baseRefs,
            newBranches);
        var mainPrUrl = await exporter.Run();
        var linkText = string.Format(Texts.MAIN_PR_LINK_TEMPLATE, mainPrUrl);
        var form = new SuccessWindow("Export finished", linkText, mainPrUrl);
        await ShowWindowWithoutActivation(form);
    }

    async Task RunCsprojSetup()
    {
        SaveSettings();
        await CsprojApp.Run(
            _settings.UnityPath,
            _settings.CsprojProjectPath,
            _settings.CsprojRunUnity,
            _settings.CsprojMakeCommits,
            _settings.CsprojPushWhenDone);
        await ShowMessage("CsprojSetup finished");
    }

    async Task RunFlatten()
    {
        SaveSettings();

        var repoPath = Helpers.GetRuntimeRepositoryPath(
            Texts.SETUP_FOLDER,
            _settings.SetupTargetRepoUrl);

        await CloneRepository(_settings.SetupTargetRepoUrl, repoPath);
        var tempBranch = $"temp-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await Helpers.CreateAndCheckoutBranch(repoPath, tempBranch, _settings.Pat, true);
        await FetchBranch(repoPath, _settings.SetupSourceRepoUrl, _settings.SetupSourceBranch);

        var flattener = new SubmodulesFlattener(_settings.Pat);
        await flattener.Run(repoPath, _settings.SetupSourceBranch, _settings.SetupTargetBranch);

        _settings.CsprojProjectPath = repoPath;
        _csprojProjectPathBox.Text = repoPath;
        SaveSettings();

        await ShowMessage("Flatten finished; regenerate project files in Unity then run Csproj relink");
    }

    async Task RunCsprojRelink()
    {
        SaveSettings();
        await CsprojApp.Run(
            _settings.UnityPath,
            _settings.CsprojProjectPath,
            false,
            _settings.CsprojMakeCommits,
            _settings.CsprojPushWhenDone);
        await ShowMessage("Csproj relink finished");
    }

    async Task UpdateSetupBranches()
    {
        _isInternalChange = true;

        _setupSourceBox.ItemsSource = Array.Empty<string>();

        if (string.IsNullOrEmpty(_setupSourceRepoUrlBox.Text) == false)
        {
            var sourceBranches = await Helpers.GetRemoteBranches(_setupSourceRepoUrlBox.Text,
                _setupPatBox.Text);
            _setupSourceBox.ItemsSource = sourceBranches;
        }

        _setupSourceBox.SelectedItem = _settings.SetupSourceBranch;
        _setupTargetBox.Text = _settings.SetupTargetBranch;
        _isInternalChange = false;
    }

    async Task UpdateExportBranches()
        => await UpdateExportBranches(true, true);

    async Task UpdateExportBranches(bool refreshSource, bool refreshTarget)
    {
        var previousSourceSelection = _exportSourceBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(previousSourceSelection))
            previousSourceSelection = _settings.ExportSourceBranch;

        var previousTargetSelection = _exportTargetBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(previousTargetSelection))
            previousTargetSelection = _settings.ExportTargetBranch;

        _isInternalChange = true;

        try
        {
            if (refreshSource)
            {
                _exportSourceBox.ItemsSource = Array.Empty<string>();

                if (string.IsNullOrEmpty(_exportSourceRepoUrlBox.Text) == false)
                {
                    var sourceBranches = await Helpers.GetRemoteBranches(_exportSourceRepoUrlBox.Text,
                        _exportPatBox.Text);
                    _exportSourceBox.ItemsSource = sourceBranches;

                    if (string.IsNullOrEmpty(previousSourceSelection) == false
                        && sourceBranches.Contains(previousSourceSelection) == false)
                        previousSourceSelection = sourceBranches.FirstOrDefault();
                }

                _exportSourceBox.SelectedItem = string.IsNullOrEmpty(previousSourceSelection) == false
                    ? previousSourceSelection
                    : null;
            }

            if (refreshTarget)
            {
                _exportTargetBox.ItemsSource = Array.Empty<string>();

                if (string.IsNullOrEmpty(_exportTargetRepoUrlBox.Text) == false)
                {
                    var targetBranches = await Helpers.GetRemoteBranches(_exportTargetRepoUrlBox.Text,
                        _exportPatBox.Text);
                    _exportTargetBox.ItemsSource = targetBranches;

                    if (string.IsNullOrEmpty(previousTargetSelection) == false
                        && targetBranches.Contains(previousTargetSelection) == false)
                        previousTargetSelection = targetBranches.FirstOrDefault();
                }

                _exportTargetBox.SelectedItem = string.IsNullOrEmpty(previousTargetSelection) == false
                    ? previousTargetSelection
                    : null;
            }
        }
        finally
        {
            _isInternalChange = false;
        }

        SaveSettings();
    }

    async Task CloneRepository(string url, string path)
        => await Helpers.PrepareRepositoryCache(url, path, _settings.Pat);

    async Task FetchBranch(string path, string url, string branch)
    {
        Logger.Write(string.Format("Fetching {0} from {1}", branch, url));
        await Helpers.EnsureRemote(path, Texts.SOURCE_REMOTE, url, _settings.Pat);
        var fetchCommand = string.Format(CultureInfo.InvariantCulture,
            "-C {0} fetch --depth 1 --force {1} {2}:{2}",
            path,
            Texts.SOURCE_REMOTE,
            branch);
        await Helpers.RunGit(fetchCommand, _settings.Pat);
        await Helpers.ConfigureBranchRemote(path, branch, Texts.SOURCE_REMOTE, _settings.Pat);
    }

    async Task CheckDiffs()
    {
        _checkDiffsButton.IsEnabled = false;
        _runExportButton.IsEnabled = false;
        _submodulePanel.Children.Clear();
        _submoduleBranchBoxes.Clear();
        _submoduleMakeBranchChecks.Clear();
        _submoduleNewBranchBoxes.Clear();

        try
        {
            ClearExportFolder();

            var tool = new ExportTool(
                _exportPatBox.Text,
                _exportSourceRepoUrlBox.Text ?? string.Empty,
                _exportTargetRepoUrlBox.Text ?? string.Empty,
                _exportSourceBox.SelectedItem?.ToString() ?? string.Empty,
                _exportTargetBox.SelectedItem?.ToString() ?? string.Empty);

            _diffSubmodules = await tool.GetChangedSubmodules();

            foreach (var submodule in _diffSubmodules)
            {
                var combo = new ComboBox { Width = BOX_WIDTH };
                var branches = await Helpers.GetRemoteBranches(submodule.Url, _exportPatBox.Text);
                _settings.ExportSubmoduleSettings.TryGetValue(submodule.Name, out var savedSetting);
                if (savedSetting != null
                    && string.IsNullOrWhiteSpace(savedSetting.BaseBranch) == false
                    && branches.Contains(savedSetting.BaseBranch) == false)
                    branches.Add(savedSetting.BaseBranch);

                _submoduleBranchBoxes[submodule.Name] = combo;

                combo.ItemsSource = branches;
                combo.SelectionChanged += (_, _) =>
                {
                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                };

                var makeBranch = new CheckBox { Content = "Make new branch" };
                var branchBox = new TextBox { Width = 150, IsVisible = false };

                _submoduleMakeBranchChecks[submodule.Name] = makeBranch;
                _submoduleNewBranchBoxes[submodule.Name] = branchBox;

                makeBranch.IsCheckedChanged += (_, _) =>
                {
                    var isChecked = makeBranch.IsChecked == true;
                    combo.IsVisible = isChecked == false;
                    branchBox.IsVisible = isChecked;
                    if (isChecked && string.IsNullOrWhiteSpace(branchBox.Text))
                        branchBox.Text = branches.Contains(_exportTargetBox.SelectedItem?.ToString() ?? string.Empty)
                            ? string.Empty
                            : _exportTargetBox.SelectedItem?.ToString();

                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                };

                branchBox.GetObservable(TextBox.TextProperty).Subscribe(_ =>
                {
                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                });

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = $"{submodule.Name} base branch" });
                row.Children.Add(combo);
                row.Children.Add(makeBranch);
                row.Children.Add(branchBox);
                _submodulePanel.Children.Add(row);

                if (savedSetting != null)
                {
                    _isInternalChange = true;

                    if (savedSetting.IsCreatingNewBranch)
                    {
                        branchBox.Text = savedSetting.NewBranchName;
                        makeBranch.IsChecked = true;
                    }
                    else
                    {
                        combo.SelectedItem = savedSetting.BaseBranch;
                        makeBranch.IsChecked = false;
                    }

                    _isInternalChange = false;
                }
            }

            SetRunExportEnabled();

            string message;
            if (_diffSubmodules.Count > 0)
            {
                var names = string.Join(", ", _diffSubmodules.Select(s => s.Name));
                message = string.Format(CultureInfo.InvariantCulture,
                    "Found diffs with the following submodules: ({0}) please pick a base branch for each",
                    names);
            }
            else
            {
                message = "There are no submodule diffs, you may proceed with the Export";
            }

            await ShowMessage(message);
        }
        finally
        {
            _checkDiffsButton.IsEnabled = true;
        }
    }

    void ClearExportFolder()
    {
        var runtime = Helpers.PrepareRuntime();
        var exportPath = Path.Combine(runtime, Texts.EXPORT_FOLDER);

        if (Directory.Exists(exportPath))
            Helpers.DeleteDirectory(exportPath);

        Directory.CreateDirectory(exportPath);
    }

    void SetRunExportEnabled()
    {
        if (_submoduleBranchBoxes.Count == 0)
        {
            _runExportButton.IsEnabled = true;
            return;
        }

        foreach (var submodule in _diffSubmodules)
        {
            if (_submoduleMakeBranchChecks.TryGetValue(submodule.Name, out var makeBranchCheck) == false
                || _submoduleNewBranchBoxes.TryGetValue(submodule.Name, out var newBranchBox) == false
                || _submoduleBranchBoxes.TryGetValue(submodule.Name, out var branchBox) == false)
            {
                _runExportButton.IsEnabled = false;
                return;
            }

            var isMakeNewBranch = makeBranchCheck.IsChecked == true;
            if (isMakeNewBranch)
            {
                if (string.IsNullOrWhiteSpace(newBranchBox.Text))
                {
                    _runExportButton.IsEnabled = false;
                    return;
                }
            }
            else if (branchBox.SelectedItem == null)
            {
                _runExportButton.IsEnabled = false;
                return;
            }
        }

        _runExportButton.IsEnabled = true;
    }

    async Task DeleteSetupCache()
        => await DeleteCachedRepository(Texts.SETUP_FOLDER, _settings.SetupTargetRepoUrl);

    async Task DeleteExportCache()
        => await DeleteCachedRepository(Texts.EXPORT_FOLDER, _settings.ExportTargetRepoUrl);

    async Task DeleteCachedRepository(string scope, string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            await ShowMessage("Please enter a repository URL before deleting the cached repository.");
            return;
        }

        var runtime = Helpers.PrepareRuntime();
        var repoName = RepoUtils.GetRepoName(repoUrl);
        var repoPath = Path.Combine(runtime, scope, repoName);

        if (Directory.Exists(repoPath) == false)
        {
            await ShowMessage(string.Format(CultureInfo.InvariantCulture,
                "No cached repository found at {0}.", repoPath));
            return;
        }

        Helpers.DeleteDirectory(repoPath);

        await ShowMessage(string.Format(CultureInfo.InvariantCulture,
            "Cached repository deleted at {0}.", repoPath));
    }
}
