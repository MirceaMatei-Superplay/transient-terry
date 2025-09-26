using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Common.Scripts;
using ExportTool = SubmodulesDeflattenerExport.Scripts.SubmodulesDeflattenerExport;
using SetupRunner = SubmodulesFlattenerSetup.Scripts.SubmodulesFlattenerSetup;
using CsprojApp = CsprojSetupToolApp.Apps.CsprojSetupToolApp;
using System;
using System.Collections;
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
        _activeOperationStopwatch = stopwatch;

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
        finally
        {
            _activeOperationStopwatch = null;
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
                var branches = await Helpers.GetRemoteBranches(submodule.Url, _exportPatBox.Text);
                _settings.ExportSubmoduleSettings.TryGetValue(submodule.Name, out var savedSetting);
                if (savedSetting != null
                    && string.IsNullOrWhiteSpace(savedSetting.BaseBranch) == false
                    && branches.Contains(savedSetting.BaseBranch) == false)
                    branches.Add(savedSetting.BaseBranch);

                var card = CreateSubmoduleCard(submodule, branches, false);
                _submodulePanel.Children.Add(card.container);

                _submoduleBranchBoxes[submodule.Name] = card.branchCombo;
                _submoduleMakeBranchChecks[submodule.Name] = card.createBranchCheck;
                _submoduleNewBranchBoxes[submodule.Name] = card.newBranchBox;

                card.branchCombo.SelectionChanged += (_, _) =>
                {
                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                };

                card.createBranchCheck.IsCheckedChanged += (_, _) =>
                {
                    var isChecked = card.createBranchCheck.IsChecked == true;
                    if (isChecked && string.IsNullOrWhiteSpace(card.newBranchBox.Text))
                    {
                        var targetBranch = _exportTargetBox.SelectedItem?.ToString();
                        var hasTarget = targetBranch != null
                            && branches.Contains(targetBranch);
                        card.newBranchBox.Text = hasTarget
                            ? string.Empty
                            : targetBranch ?? string.Empty;
                    }

                    UpdateSubmoduleCardVisibility(card.branchCombo, card.createBranchCheck, card.newBranchBox);
                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                };

                card.newBranchBox.GetObservable(TextBox.TextProperty).Subscribe(_ =>
                {
                    SetRunExportEnabled();
                    HandleSubmoduleValueChanged();
                });

                UpdateSubmoduleCardVisibility(card.branchCombo, card.createBranchCheck, card.newBranchBox);

                if (savedSetting != null)
                {
                    _isInternalChange = true;

                    if (savedSetting.IsCreatingNewBranch)
                    {
                        card.newBranchBox.Text = savedSetting.NewBranchName;
                        card.createBranchCheck.IsChecked = true;
                    }
                    else
                    {
                        card.branchCombo.SelectedItem = savedSetting.BaseBranch;
                        card.createBranchCheck.IsChecked = false;
                    }

                    UpdateSubmoduleCardVisibility(card.branchCombo, card.createBranchCheck, card.newBranchBox);
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

    (Border container, ComboBox branchCombo, CheckBox createBranchCheck, TextBox newBranchBox)
        CreateSubmoduleCard(SubmoduleInfo submodule, IReadOnlyList<string> branches, bool isPreview)
    {
        var cardBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            BorderBrush = new SolidColorBrush(Color.Parse("#323232")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var layout = new StackPanel { Spacing = 12 };

        var header = new StackPanel { Spacing = 2 };
        header.Children.Add(new TextBlock
        {
            Text = submodule.Name,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = submodule.Path,
            Foreground = new SolidColorBrush(Color.Parse("#C5C5C5")),
            FontSize = 12
        });
        header.Children.Add(new TextBlock
        {
            Text = submodule.Url,
            Foreground = new SolidColorBrush(Color.Parse("#8C8C8C")),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        layout.Children.Add(header);

        layout.Children.Add(new TextBlock
        {
            Text = "Select a base branch or create a new branch for this submodule export.",
            Foreground = new SolidColorBrush(Color.Parse("#BBBBBB")),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        var branchRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,12,*"),
            RowDefinitions = new RowDefinitions("Auto")
        };

        branchRow.Children.Add(new TextBlock
        {
            Text = "Base branch",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        });

        var branchCombo = new ComboBox
        {
            MinWidth = BOX_WIDTH,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = branches
        };
        if (isPreview)
            branchCombo.IsEnabled = false;

        Grid.SetColumn(branchCombo, 2);
        branchRow.Children.Add(branchCombo);
        layout.Children.Add(branchRow);

        var createBranchCheck = new CheckBox
        {
            Content = "Create new branch",
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isPreview)
            createBranchCheck.IsEnabled = false;

        var newBranchPanel = new StackPanel { Spacing = 6 };
        newBranchPanel.Children.Add(createBranchCheck);

        var newBranchBox = new TextBox
        {
            MinWidth = BOX_WIDTH,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsVisible = false,
            Watermark = "New branch name",
            Margin = new Thickness(24, 0, 0, 0)
        };
        if (isPreview)
            newBranchBox.IsEnabled = false;

        newBranchPanel.Children.Add(newBranchBox);
        layout.Children.Add(newBranchPanel);

        cardBorder.Child = layout;

        return (cardBorder, branchCombo, createBranchCheck, newBranchBox);
    }

    void UpdateSubmoduleCardVisibility(ComboBox branchCombo, CheckBox createBranchCheck, TextBox newBranchBox)
    {
        var isCreatingBranch = createBranchCheck.IsChecked == true;
        branchCombo.IsVisible = isCreatingBranch == false;
        newBranchBox.IsVisible = isCreatingBranch;
    }

    void PopulateDebugSubmodulePanel(IReadOnlyList<SubmoduleInfo> submodules, bool useSampleData)
    {
        _debugSubmodulePanel.Children.Clear();

        if (submodules.Count == 0)
        {
            _debugSubmodulePanel.Children.Add(new TextBlock
            {
                Text = useSampleData
                    ? "Sample submodule data is unavailable."
                    : "No submodule diffs are currently loaded.",
                Foreground = new SolidColorBrush(Color.Parse("#BBBBBB")),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        for (var index = 0; index < submodules.Count; index++)
        {
            var submodule = submodules[index];
            IReadOnlyList<string> branches;

            if (useSampleData)
            {
                branches = SAMPLE_BRANCHES;
            }
            else if (_submoduleBranchBoxes.TryGetValue(submodule.Name, out var existingCombo))
            {
                branches = ExtractBranchOptions(existingCombo);
            }
            else
            {
                branches = Array.Empty<string>();
            }

            var card = CreateSubmoduleCard(submodule, branches, true);
            _debugSubmodulePanel.Children.Add(card.container);

            if (useSampleData)
            {
                var sampleBranch = index < branches.Count ? branches[index] : branches.FirstOrDefault();
                card.branchCombo.SelectedItem = sampleBranch;

                if (index == 1)
                {
                    card.createBranchCheck.IsChecked = true;
                    card.newBranchBox.Text = "feature/localization-sync";
                }
                else if (index == 2)
                {
                    card.createBranchCheck.IsChecked = true;
                    card.newBranchBox.Text = "hotfix/ui-polish";
                }
                else
                {
                    card.createBranchCheck.IsChecked = false;
                }
            }
            else
            {
                if (_submoduleMakeBranchChecks.TryGetValue(submodule.Name, out var makeBranchCheck)
                    && makeBranchCheck.IsChecked == true)
                {
                    card.createBranchCheck.IsChecked = true;
                    card.newBranchBox.Text = _submoduleNewBranchBoxes.TryGetValue(submodule.Name, out var branchBox)
                        ? branchBox?.Text ?? string.Empty
                        : string.Empty;
                }
                else if (_submoduleBranchBoxes.TryGetValue(submodule.Name, out var baseBranchBox)
                    && baseBranchBox.SelectedItem != null)
                {
                    card.branchCombo.SelectedItem = baseBranchBox.SelectedItem;
                }
            }

            UpdateSubmoduleCardVisibility(card.branchCombo, card.createBranchCheck, card.newBranchBox);
        }
    }

    List<SubmoduleInfo> BuildSampleSubmodules()
        => SAMPLE_SUBMODULES.ToList();

    static IReadOnlyList<string> ExtractBranchOptions(ComboBox combo)
    {
        if (combo.ItemsSource is IEnumerable enumerable)
        {
            return enumerable
                .Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(text => string.IsNullOrWhiteSpace(text) == false)
                .Distinct()
                .ToList();
        }

        return Array.Empty<string>();
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
