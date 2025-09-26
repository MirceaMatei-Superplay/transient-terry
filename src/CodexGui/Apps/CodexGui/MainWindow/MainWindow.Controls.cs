using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Common.Scripts;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
    void AttachControls()
    {
        _unityPathBox = this.FindControl<TextBox>("unityPathBox")!;
        _setupSourceRepoUrlBox = this.FindControl<TextBox>("setupSourceRepoUrlBox")!;
        _setupTargetRepoUrlBox = this.FindControl<TextBox>("setupTargetRepoUrlBox")!;
        _setupSourceBox = this.FindControl<ComboBox>("setupSourceBox")!;
        _setupTargetBox = this.FindControl<TextBox>("setupTargetBox")!;
        _setupPatBox = this.FindControl<TextBox>("setupPatBox")!;
        _openSetupFolderButton = this.FindControl<Button>("openSetupFolderButton")!;
        _deleteSetupCacheButton = this.FindControl<Button>("deleteSetupCacheButton")!;
        _runSetupButton = this.FindControl<Button>("runSetupButton")!;
        _refreshSetupSourceButton = this.FindControl<Button>("refreshSetupSourceButton")!;
        _exportSourceRepoUrlBox = this.FindControl<TextBox>("exportSourceRepoUrlBox")!;
        _exportTargetRepoUrlBox = this.FindControl<TextBox>("exportTargetRepoUrlBox")!;
        _exportSourceBox = this.FindControl<ComboBox>("exportSourceBox")!;
        _exportTargetBox = this.FindControl<ComboBox>("exportTargetBox")!;
        _exportPatBox = this.FindControl<TextBox>("exportPatBox")!;
        _openExportFolderButton = this.FindControl<Button>("openExportFolderButton")!;
        _deleteExportCacheButton = this.FindControl<Button>("deleteExportCacheButton")!;
        _refreshExportSourceButton = this.FindControl<Button>("refreshExportSourceButton")!;
        _refreshExportTargetButton = this.FindControl<Button>("refreshExportTargetButton")!;
        _checkDiffsButton = this.FindControl<Button>("checkDiffsButton")!;
        _submodulePanel = this.FindControl<StackPanel>("submodulePanel")!;
        _runExportButton = this.FindControl<Button>("runExportButton")!;
        _showCurrentSubmoduleCardsButton = this.FindControl<Button>("showCurrentSubmoduleCardsButton")!;
        _showSampleSubmoduleCardsButton = this.FindControl<Button>("showSampleSubmoduleCardsButton")!;
        _debugSubmodulePanel = this.FindControl<StackPanel>("debugSubmodulePanel")!;
        _csprojProjectPathBox = this.FindControl<TextBox>("csprojProjectPathBox")!;
        _csprojRunUnityBox = this.FindControl<CheckBox>("csprojRunUnityBox")!;
        _csprojMakeCommitsBox = this.FindControl<CheckBox>("csprojMakeCommitsBox")!;
        _csprojPushWhenDoneBox = this.FindControl<CheckBox>("csprojPushWhenDoneBox")!;
        _runCsprojSetupButton = this.FindControl<Button>("runCsprojSetupButton")!;
        _runFlattenButton = this.FindControl<Button>("runFlattenButton")!;
        _runCsprojRelinkButton = this.FindControl<Button>("runCsprojRelinkButton")!;
        _logItemsControl = this.FindControl<ItemsControl>("logItemsControl")!;
        _summaryItemsControl = this.FindControl<ItemsControl>("summaryItemsControl")!;
        _logScrollViewer = this.FindControl<ScrollViewer>("logScrollViewer")!;
        _logSummaryText = this.FindControl<TextBlock>("logSummaryText")!;
        _callstackOverlay = this.FindControl<CallstackOverlay>("callstackOverlay")!;
        _messageOverlay = this.FindControl<MessageOverlay>("messageOverlay")!;
    }

    void RegisterEvents()
    {
        _unityPathBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnValueChanged());
        _setupSourceRepoUrlBox.GetObservable(TextBox.TextProperty)
            .Subscribe(_ => OnRepoUrlChanged(_setupSourceRepoUrlBox));
        _setupTargetRepoUrlBox.GetObservable(TextBox.TextProperty)
            .Subscribe(_ => OnRepoUrlChanged(_setupTargetRepoUrlBox));
        _setupSourceBox.GetObservable(ComboBox.SelectedItemProperty).Subscribe(_ => OnValueChanged());
        _setupTargetBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnValueChanged());
        _exportSourceRepoUrlBox.GetObservable(TextBox.TextProperty)
            .Subscribe(_ => OnRepoUrlChanged(_exportSourceRepoUrlBox));
        _exportTargetRepoUrlBox.GetObservable(TextBox.TextProperty)
            .Subscribe(_ => OnRepoUrlChanged(_exportTargetRepoUrlBox));
        _exportSourceBox.GetObservable(ComboBox.SelectedItemProperty).Subscribe(_ => OnValueChanged());
        _exportTargetBox.GetObservable(ComboBox.SelectedItemProperty).Subscribe(_ => OnValueChanged());
        _setupPatBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnPatChanged(_setupPatBox));
        _exportPatBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnPatChanged(_exportPatBox));
        _csprojProjectPathBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnValueChanged());
        _csprojRunUnityBox.GetObservable(CheckBox.IsCheckedProperty).Subscribe(_ => OnValueChanged());
        _csprojMakeCommitsBox.GetObservable(CheckBox.IsCheckedProperty).Subscribe(_ => OnValueChanged());
        _csprojPushWhenDoneBox.GetObservable(CheckBox.IsCheckedProperty).Subscribe(_ => OnValueChanged());

        _openSetupFolderButton.Click += OnOpenSetupFolder;
        _deleteSetupCacheButton.Click += async (_, _) => await DeleteSetupCache();
        _runSetupButton.Click += async (_, _) => await ExecuteOperation("Run Full Setup", RunSetup);
        _refreshSetupSourceButton.Click += async (_, _) => await UpdateSetupBranches();
        _openExportFolderButton.Click += OnOpenExportFolder;
        _deleteExportCacheButton.Click += async (_, _) => await DeleteExportCache();
        _runExportButton.Click += async (_, _) => await ExecuteOperation("Run Export", RunExport);
        _checkDiffsButton.Click += async (_, _) => await ExecuteOperation("Check Diffs", CheckDiffs);
        _refreshExportSourceButton.Click += async (_, _) => await UpdateExportBranches(true, false);
        _refreshExportTargetButton.Click += async (_, _) => await UpdateExportBranches(false, true);
        _showCurrentSubmoduleCardsButton.Click += OnShowCurrentSubmoduleCards;
        _showSampleSubmoduleCardsButton.Click += OnShowSampleSubmoduleCards;
        _runCsprojSetupButton.Click += async (_, _) => await ExecuteOperation("Run Csproj Setup", RunCsprojSetup);
        _runFlattenButton.Click += async (_, _) => await ExecuteOperation("Run Flatten", RunFlatten);
        _runCsprojRelinkButton.Click += async (_, _) => await ExecuteOperation("Run Csproj Relink", RunCsprojRelink);

        _summaryBoundsSubscription = _summaryItemsControl
            .GetObservable(BoundsProperty)
            .Subscribe(OnSummaryBoundsChanged);
    }

    async void OnBrowseUnity(object? sender, RoutedEventArgs e)
        => await BrowseForUnityPath(_unityPathBox);

    async void OnBrowseProject(object? sender, RoutedEventArgs e)
        => await BrowseForFolder(_csprojProjectPathBox);

    async void OnShowDebugInfoMessage(object? sender, RoutedEventArgs e)
        => await ShowMessage(
            "This is a sample information message to test the overlay presentation.",
            "Info Message",
            "ℹ");

    async void OnShowDebugWarningMessage(object? sender, RoutedEventArgs e)
        => await ShowMessage(
            "This is a sample warning to confirm that highlighting appears correctly.",
            "Warning Message",
            "⚠",
            "#F2B84B",
            "#3D2B0A");

    async void OnShowDebugErrorMessage(object? sender, RoutedEventArgs e)
    {
        var callstack = string.Join(Environment.NewLine,
            new[]
            {
                "System.InvalidOperationException: Simulated failure while processing input.",
                string.Concat(
                    @"   at TransientTerry.Tools.DemoProcessor.Run() in C:\Projects\TransientTerry\",
                    "DemoProcessor.cs:line 42"),
                "   at TransientTerry.Tools.Program.Main()"
            });

        await ShowMessage(
            "This is a sample error message to test the overlay, including a callstack for review.",
            "Error Message",
            "✖",
            "#F47070",
            "#3A1010",
            callstack);
    }

    async void OnShowCurrentSubmoduleCards(object? sender, RoutedEventArgs e)
    {
        PopulateDebugSubmodulePanel(_diffSubmodules, false);

        if (_diffSubmodules.Count == 0)
            await ShowMessage(
                "There are no submodule diffs available. Run Check Diffs to load current data.");
    }

    void OnShowSampleSubmoduleCards(object? sender, RoutedEventArgs e)
    {
        PopulateDebugSubmodulePanel(BuildSampleSubmodules(), true);
    }

    async void OnCopyLogEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not LogDisplayEntry entry)
            return;

        await CopyToClipboard(entry.CopyText);
    }

    async void OnViewLogCallstack(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not LogDisplayEntry entry)
            return;

        if (string.IsNullOrWhiteSpace(entry.Callstack))
            return;

        var title = string.Format(CultureInfo.InvariantCulture,
            "{0} Callstack",
            entry.Title);
        await ShowCallstack(title, entry.Callstack);
    }

    void OnToggleLogMessage(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not LogDisplayEntry entry)
            return;

        entry.ToggleMessageExpansion();
    }

    async void OnCopySummaryEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not LogSummaryEntry entry)
            return;

        await CopyToClipboard(entry.CopyText);
    }

    void OnSummaryBoundsChanged(Rect bounds)
    {
        var newWidth = bounds.Width;
        if (newWidth <= 0)
            return;

        var adjustedWidth = newWidth;
        if (Math.Abs(adjustedWidth - _summaryBarWidth) < 0.5)
            return;

        _summaryBarWidth = adjustedWidth;
        UpdateSummary();
    }

    void OnValueChanged()
    {
        if (_isInternalChange)
            return;

        SaveSettings();
    }

    void HandleSubmoduleValueChanged()
    {
        if (_isInternalChange)
            return;

        SaveSettings();
    }

    async void OnRepoUrlChanged(Control sender)
    {
        if (_isInternalChange)
            return;

        SaveSettings();

        if (sender == _setupSourceRepoUrlBox || sender == _setupTargetRepoUrlBox)
            await UpdateSetupBranches();
        else
            await UpdateExportBranches();
    }

    async void OnPatChanged(Control sender)
    {
        if (_isInternalChange)
        {
            SaveSettings();
            return;
        }

        _isInternalChange = true;

        if (sender == _setupPatBox && _exportPatBox.Text != _setupPatBox.Text)
            _exportPatBox.Text = _setupPatBox.Text;
        else if (sender == _exportPatBox && _setupPatBox.Text != _exportPatBox.Text)
            _setupPatBox.Text = _exportPatBox.Text;

        _isInternalChange = false;

        SaveSettings();

        try
        {
            await UpdateSetupBranches();
            await UpdateExportBranches();
        }
        finally
        {
            SaveSettings();
        }
    }

    async void OnOpenSetupFolder(object? sender, RoutedEventArgs e)
        => await OpenFolderForScope(Texts.SETUP_FOLDER);

    async void OnOpenExportFolder(object? sender, RoutedEventArgs e)
        => await OpenFolderForScope(Texts.EXPORT_FOLDER);
}
