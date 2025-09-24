using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Common.Scripts;
using ExportTool = SubmodulesDeflattenerExport.Scripts.SubmodulesDeflattenerExport;
using SetupRunner = SubmodulesFlattenerSetup.Scripts.SubmodulesFlattenerSetup;
using CsprojApp = CsprojSetupToolApp.Apps.CsprojSetupToolApp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow : Window
{
    const string SETTINGS_FILE_NAME = "guiSettings.json";
    const int BOX_WIDTH = 400;
    const int CHECK_BOX_WIDTH = 300;
    const double DEFAULT_SUMMARY_BAR_WIDTH = 360;

    readonly Dictionary<string, ComboBox> _submoduleBranchBoxes = new();
    readonly Dictionary<string, CheckBox> _submoduleMakeBranchChecks = new();
    readonly Dictionary<string, TextBox> _submoduleNewBranchBoxes = new();
    List<SubmoduleInfo> _diffSubmodules = new();

    readonly ObservableCollection<LogDisplayEntry> _logEntries = new();
    readonly ObservableCollection<LogSummaryEntry> _summaryEntries = new();
    readonly List<LogDisplayEntry> _completedLogEntries = new();


    bool _isInternalChange;
    bool _isLoading;
    double _summaryBarWidth = DEFAULT_SUMMARY_BAR_WIDTH;

    static readonly string _settingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TransientTerry");

    static readonly string _settingsFilePath = Path.Combine(_settingsFolder, SETTINGS_FILE_NAME);

    GuiSettings _settings = new();

    TextBox _unityPathBox = null!;
    TextBox _setupSourceRepoUrlBox = null!;
    TextBox _setupTargetRepoUrlBox = null!;
    ComboBox _setupSourceBox = null!;
    TextBox _setupTargetBox = null!;
    TextBox _setupPatBox = null!;
    Button _openSetupFolderButton = null!;
    Button _deleteSetupCacheButton = null!;
    Button _runSetupButton = null!;
    Button _refreshSetupSourceButton = null!;
    TextBox _exportSourceRepoUrlBox = null!;
    TextBox _exportTargetRepoUrlBox = null!;
    ComboBox _exportSourceBox = null!;
    ComboBox _exportTargetBox = null!;
    TextBox _exportPatBox = null!;
    Button _openExportFolderButton = null!;
    Button _deleteExportCacheButton = null!;
    Button _refreshExportSourceButton = null!;
    Button _refreshExportTargetButton = null!;
    Button _checkDiffsButton = null!;
    StackPanel _submodulePanel = null!;
    Button _runExportButton = null!;
    TextBox _csprojProjectPathBox = null!;
    CheckBox _csprojRunUnityBox = null!;
    CheckBox _csprojMakeCommitsBox = null!;
    CheckBox _csprojPushWhenDoneBox = null!;
    Button _runCsprojSetupButton = null!;
    Button _runFlattenButton = null!;
    Button _runCsprojRelinkButton = null!;
    ItemsControl _logItemsControl = null!;
    ItemsControl _summaryItemsControl = null!;
    ScrollViewer _logScrollViewer = null!;
    TextBlock _logSummaryText = null!;
    MessageOverlay _messageOverlay = null!;
    CallstackOverlay _callstackOverlay = null!;
    IDisposable? _summaryBoundsSubscription;

    public MainWindow()
    {
        _isLoading = true;

        InitializeComponent();
        AttachControls();
        _messageOverlay.CallstackRequested += OnMessageCallstackRequested;
        RegisterEvents();
        InitializeLogging();
        LoadSettings();
    }

    void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

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

    async void OnBrowseUnity(object? sender, RoutedEventArgs e)
        => await BrowseForUnityPath(_unityPathBox);

    async void OnBrowseProject(object? sender, RoutedEventArgs e)
        => await BrowseForFolder(_csprojProjectPathBox);

    async void OnShowDebugInfoMessage(object? sender, RoutedEventArgs e)
        => await ShowMessage("This is a sample information message to test the overlay presentation.",
            "Info Message",
            "ℹ");

    async void OnShowDebugWarningMessage(object? sender, RoutedEventArgs e)
        => await ShowMessage("This is a sample warning to confirm that highlighting appears correctly.",
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
                "   at TransientTerry.Tools.DemoProcessor.Run() in C:\\Projects\\TransientTerry\\DemoProcessor.cs:line 42",
                "   at TransientTerry.Tools.Program.Main()"
            });

        await ShowMessage("This is a sample error message to test the overlay, including a callstack for review.",
            "Error Message",
            "✖",
            "#F47070",
            "#3A1010",
            callstack);
    }

    void InitializeLogging()
    {
        _logItemsControl.ItemsSource = _logEntries;
        _summaryItemsControl.ItemsSource = _summaryEntries;
        UpdateSummaryText();

        Logger.LogGenerated += HandleLogGenerated;
        Closed += OnClosed;
    }

    void RegisterEvents()
    {
        _unityPathBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnValueChanged());
        _setupSourceRepoUrlBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnRepoUrlChanged(_setupSourceRepoUrlBox));
        _setupTargetRepoUrlBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnRepoUrlChanged(_setupTargetRepoUrlBox));
        _setupSourceBox.GetObservable(ComboBox.SelectedItemProperty).Subscribe(_ => OnValueChanged());
        _setupTargetBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnValueChanged());
        _exportSourceRepoUrlBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnRepoUrlChanged(_exportSourceRepoUrlBox));
        _exportTargetRepoUrlBox.GetObservable(TextBox.TextProperty).Subscribe(_ => OnRepoUrlChanged(_exportTargetRepoUrlBox));
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
        _runCsprojSetupButton.Click += async (_, _) => await ExecuteOperation("Run Csproj Setup", RunCsprojSetup);
        _runFlattenButton.Click += async (_, _) => await ExecuteOperation("Run Flatten", RunFlatten);
        _runCsprojRelinkButton.Click += async (_, _) => await ExecuteOperation("Run Csproj Relink", RunCsprojRelink);

        _summaryBoundsSubscription = _summaryItemsControl
            .GetObservable(BoundsProperty)
            .Subscribe(OnSummaryBoundsChanged);
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

    async Task CopyToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
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

    async Task OpenFolderForScope(string scope)
    {
        var runtime = Helpers.PrepareRuntime();
        var scopePath = Path.Combine(runtime, scope);
        Directory.CreateDirectory(scopePath);

        if (TryOpenFolder(scopePath))
            return;

        await ShowMessage(string.Format(CultureInfo.InvariantCulture,
            "Failed to open folder at {0}.", scopePath));
    }

    bool TryOpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Win32Exception)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return TryOpenFolderWithShell(path);
    }

    bool TryOpenFolderWithShell(string path)
    {
        var command = GetFolderOpenCommand();
        if (command == null)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = command.Value.fileName,
                Arguments = string.Format(CultureInfo.InvariantCulture,
                    command.Value.argumentFormat,
                    path),
                UseShellExecute = false
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    (string fileName, string argumentFormat)? GetFolderOpenCommand()
    {
        if (OperatingSystem.IsWindows())
            return ("explorer", "\"{0}\"");
        if (OperatingSystem.IsLinux())
            return ("xdg-open", "\"{0}\"");
        if (OperatingSystem.IsMacOS())
            return ("open", "\"{0}\"");
        return null;
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

    void LoadSettings()
    {
        _isLoading = true;

        EnsureSettingsDirectory();

        var legacyPath = GetLegacySettingsPath();

        if (File.Exists(_settingsFilePath) == false && File.Exists(legacyPath))
        {
            _settings = GuiSettings.Load(legacyPath);
            _settings.Save(_settingsFilePath);
        }
        else
        {
            _settings = GuiSettings.Load(_settingsFilePath);
        }
        _unityPathBox.Text = _settings.UnityPath;
        _setupSourceRepoUrlBox.Text = _settings.SetupSourceRepoUrl;
        _setupTargetRepoUrlBox.Text = _settings.SetupTargetRepoUrl;
        _setupSourceBox.SelectedItem = _settings.SetupSourceBranch;
        _setupTargetBox.Text = _settings.SetupTargetBranch;
        _exportSourceRepoUrlBox.Text = _settings.ExportSourceRepoUrl;
        _exportTargetRepoUrlBox.Text = _settings.ExportTargetRepoUrl;
        _exportSourceBox.SelectedItem = _settings.ExportSourceBranch;
        _exportTargetBox.SelectedItem = _settings.ExportTargetBranch;
        _setupPatBox.Text = _settings.Pat;
        _exportPatBox.Text = _settings.Pat;
        _csprojProjectPathBox.Text = _settings.CsprojProjectPath;
        _csprojRunUnityBox.IsChecked = _settings.CsprojRunUnity;
        _csprojMakeCommitsBox.IsChecked = _settings.CsprojMakeCommits;
        _csprojPushWhenDoneBox.IsChecked = _settings.CsprojPushWhenDone;

        _isLoading = false;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await UpdateExportBranches();
            }
            catch (Exception exception)
            {
                Logger.Write(exception.Message);
            }
        });
    }

    void SaveSettings()
    {
        if (_isLoading)
            return;

        _settings.UnityPath = _unityPathBox.Text ?? string.Empty;
        _settings.SetupSourceRepoUrl = _setupSourceRepoUrlBox.Text ?? string.Empty;
        _settings.SetupTargetRepoUrl = _setupTargetRepoUrlBox.Text ?? string.Empty;
        _settings.SetupSourceBranch = _setupSourceBox.SelectedItem?.ToString() ?? string.Empty;
        _settings.SetupTargetBranch = _setupTargetBox.Text ?? string.Empty;
        _settings.ExportSourceRepoUrl = _exportSourceRepoUrlBox.Text ?? string.Empty;
        _settings.ExportTargetRepoUrl = _exportTargetRepoUrlBox.Text ?? string.Empty;
        _settings.ExportSourceBranch = _exportSourceBox.SelectedItem?.ToString() ?? string.Empty;
        _settings.ExportTargetBranch = _exportTargetBox.SelectedItem?.ToString() ?? string.Empty;
        _settings.Pat = _setupPatBox.Text ?? string.Empty;
        _settings.CsprojProjectPath = _csprojProjectPathBox.Text ?? string.Empty;
        _settings.CsprojRunUnity = _csprojRunUnityBox.IsChecked ?? true;
        _settings.CsprojMakeCommits = _csprojMakeCommitsBox.IsChecked ?? true;
        _settings.CsprojPushWhenDone = _csprojPushWhenDoneBox.IsChecked ?? true;

        _settings.ExportSubmoduleSettings ??= new Dictionary<string, SubmoduleExportSettings>();

        foreach (var (name, check) in _submoduleMakeBranchChecks)
        {
            var submoduleSettings = new SubmoduleExportSettings
            {
                IsCreatingNewBranch = check.IsChecked == true,
                BaseBranch = string.Empty,
                NewBranchName = string.Empty
            };

            if (submoduleSettings.IsCreatingNewBranch)
            {
                if (_submoduleNewBranchBoxes.TryGetValue(name, out var newBranchBox))
                    submoduleSettings.NewBranchName = newBranchBox.Text ?? string.Empty;
            }
            else if (_submoduleBranchBoxes.TryGetValue(name, out var branchBox)
                && branchBox.SelectedItem != null)
            {
                submoduleSettings.BaseBranch = branchBox.SelectedItem.ToString() ?? string.Empty;
            }

            _settings.ExportSubmoduleSettings[name] = submoduleSettings;
        }

        EnsureSettingsDirectory();

        _settings.Save(_settingsFilePath);
    }

    async Task BrowseForFolder(TextBox target)
    {
#pragma warning disable CS0618
        var dialog = new OpenFolderDialog();
        var result = await dialog.ShowAsync(this);
#pragma warning restore CS0618
        if (result != null)
            target.Text = result;
    }

    async Task BrowseForUnityPath(TextBox target)
    {
#pragma warning disable CS0618
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters = { new FileDialogFilter { Name = "Executable files", Extensions = { "exe" } } }
        };
#pragma warning restore CS0618

        if (File.Exists(target.Text))
            dialog.Directory = Path.GetDirectoryName(target.Text);
        else if (Directory.Exists(target.Text))
            dialog.Directory = target.Text;

        var result = await dialog.ShowAsync(this);
        if (result?.Length > 0)
        {
            target.Text = result[0];
            return;
        }

        await BrowseForFolder(target);
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
        await Helpers.RunGit($"-C {path} fetch --depth 1 --force {Texts.SOURCE_REMOTE} {branch}:{branch}", _settings.Pat);
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
                message = $"Found diffs with the following submodules: ({names}) please pick a base branch for each";
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

    async Task ShowMessage(string message,
        string title = "Message",
        string iconGlyph = "ℹ",
        string accentColor = "#42D77D",
        string accentBackground = "#214329",
        string? callstack = null)
    {
        await _messageOverlay.ShowAsync(message,
            title,
            iconGlyph,
            accentColor,
            accentBackground,
            callstack);
    }

    Task ShowCallstack(string title, string callstack)
        => _callstackOverlay.ShowAsync(title, callstack);

    async void OnMessageCallstackRequested(object? sender, MessageOverlay.CallstackRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Callstack))
            return;

        var title = string.Format(CultureInfo.InvariantCulture,
            "{0} Callstack",
            e.Title);
        await ShowCallstack(title, e.Callstack);
    }

    Task ShowWindowWithoutActivation(Window dialog)
    {
        dialog.ShowActivated = false;
        dialog.ShowInTaskbar = false;

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleClosed(object? sender, EventArgs _) 
        {
            dialog.Closed -= HandleClosed;
            completionSource.TrySetResult(null);
        }

        dialog.Closed += HandleClosed;

        dialog.Show(this);

        return completionSource.Task;
    }

    void HandleLogGenerated(LogEvent logEvent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var entry = new LogDisplayEntry(logEvent);
            _logEntries.Add(entry);

            if (logEvent.Duration.HasValue)
            {
                _completedLogEntries.Add(entry);
                UpdateSummary();
            }

            UpdateSummaryText();
            ScrollLogToEnd();
        });
    }

    void UpdateSummary()
    {
        var ordered = _completedLogEntries
            .OrderByDescending(log => log.DurationMilliseconds)
            .ToList();

        var maxDuration = ordered.FirstOrDefault()?.DurationMilliseconds ?? 0;
        var barWidth = _summaryBarWidth > 0
            ? _summaryBarWidth
            : DEFAULT_SUMMARY_BAR_WIDTH;

        _summaryEntries.Clear();
        foreach (var entry in ordered)
            _summaryEntries.Add(new LogSummaryEntry(entry, maxDuration, barWidth));
    }

    void UpdateSummaryText()
    {
        if (_logSummaryText == null)
            return;

        var totalEvents = _logEntries.Count;
        if (_completedLogEntries.Count == 0)
        {
            _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} events logged",
                totalEvents);
            return;
        }

        var ordered = _completedLogEntries
            .Where(entry => entry.DurationMilliseconds > 0)
            .OrderBy(entry => entry.DurationMilliseconds)
            .ToList();

        if (ordered.Count == 0)
        {
            _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} events logged",
                totalEvents);
            return;
        }

        var shortest = ordered.First();
        var longest = ordered.Last();

        _logSummaryText.Text = string.Format(CultureInfo.InvariantCulture,
            "{0} events • Longest: {1} ({2}) • Shortest: {3} ({4})",
            totalEvents,
            longest.SummaryLabel,
            FormatDuration(TimeSpan.FromMilliseconds(longest.DurationMilliseconds)),
            shortest.SummaryLabel,
            FormatDuration(TimeSpan.FromMilliseconds(shortest.DurationMilliseconds)));
    }

    void ScrollLogToEnd()
    {
        if (_logScrollViewer == null)
            return;

        var extent = _logScrollViewer.Extent;
        _logScrollViewer.Offset = new Vector(_logScrollViewer.Offset.X, extent.Height);
    }

    void OnClosed(object? sender, EventArgs e)
    {
        Logger.LogGenerated -= HandleLogGenerated;
        _summaryBoundsSubscription?.Dispose();
        _messageOverlay.CallstackRequested -= OnMessageCallstackRequested;
    }

    static void EnsureSettingsDirectory()
    {
        if (Directory.Exists(_settingsFolder))
            return;

        Directory.CreateDirectory(_settingsFolder);
    }

    static string GetLegacySettingsPath()
        => Path.Combine(AppContext.BaseDirectory, SETTINGS_FILE_NAME);

    static string FormatDuration(TimeSpan duration)
    {
        var totalSeconds = duration.TotalSeconds;

        if (Math.Abs(totalSeconds) >= 60)
        {
            var minutes = (int)(totalSeconds / 60);
            var secondsRemainder = totalSeconds - (minutes * 60);

            var minuteLabel = minutes == 1
                ? "1 min"
                : string.Format(CultureInfo.InvariantCulture, "{0} min", minutes);

            var secondsLabel = GetSecondsLabel(secondsRemainder);

            return string.Format(CultureInfo.InvariantCulture,
                "{0}, {1}",
                minuteLabel,
                secondsLabel);
        }

        return GetSecondsLabel(totalSeconds);
    }

    static string GetSecondsLabel(double seconds)
    {
        if (Math.Abs(seconds) < 1e-9)
            return "0 seconds";

        if (Math.Abs(seconds - 1) < 1e-9)
            return "1 second";

        return string.Format(CultureInfo.InvariantCulture,
            "{0:0.##} seconds",
            seconds);
    }

    sealed class LogDisplayEntry : INotifyPropertyChanged
    {
        const int SUMMARY_LABEL_LENGTH = 96;
        const int COLLAPSED_MESSAGE_MAX_LINES = 10;
        const char COLLAPSED_SUFFIX = '…';

        static readonly IBrush InfoBackgroundBrush = new SolidColorBrush(Color.Parse("#1F1F2A"));
        static readonly IBrush InfoBorderBrush = new SolidColorBrush(Color.Parse("#2D2D3A"));
        static readonly IBrush InfoAccentBrush = new SolidColorBrush(Color.Parse("#4D8DFF"));

        static readonly IBrush SuccessBackgroundBrush = new SolidColorBrush(Color.Parse("#14241B"));
        static readonly IBrush SuccessBorderBrush = new SolidColorBrush(Color.Parse("#205136"));
        static readonly IBrush SuccessAccentBrush = new SolidColorBrush(Color.Parse("#32C671"));

        static readonly IBrush WarningBackgroundBrush = new SolidColorBrush(Color.Parse("#2C2513"));
        static readonly IBrush WarningBorderBrush = new SolidColorBrush(Color.Parse("#5A4C22"));
        static readonly IBrush WarningAccentBrush = new SolidColorBrush(Color.Parse("#E0B341"));

        static readonly IBrush ErrorBackgroundBrush = new SolidColorBrush(Color.Parse("#2C1B1B"));
        static readonly IBrush ErrorBorderBrush = new SolidColorBrush(Color.Parse("#5A2A2A"));
        static readonly IBrush ErrorAccentBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));

        public LogDisplayEntry(LogEvent logEvent)
        {
            Title = logEvent.Title;
            Message = logEvent.Message ?? string.Empty;
            Level = logEvent.Level;
            Duration = logEvent.Duration;
            Timestamp = logEvent.Timestamp;
            Callstack = string.IsNullOrWhiteSpace(logEvent.Callstack)
                ? string.Empty
                : logEvent.Callstack.Trim();

            var normalizedMessage = NormalizeLineEndings(Message);
            var lines = normalizedMessage.Length == 0
                ? Array.Empty<string>()
                : normalizedMessage.Split('\n');

            _hasExpandableMessage = lines.Length > COLLAPSED_MESSAGE_MAX_LINES;
            _collapsedMessage = _hasExpandableMessage
                ? BuildCollapsedMessage(lines)
                : Message;
        }

        public string Title { get; }

        public string Message { get; }

        public string DisplayMessage
        {
            get
            {
                if (_hasExpandableMessage == false || _isMessageExpanded)
                    return Message;

                return _collapsedMessage;
            }
        }

        public LogLevel Level { get; }

        public TimeSpan? Duration { get; }

        public DateTimeOffset Timestamp { get; }

        public string Callstack { get; }

        public bool HasCallstack => string.IsNullOrWhiteSpace(Callstack) == false;

        public string DurationDisplay
            => Duration.HasValue
                ? FormatDuration(Duration.Value)
                : string.Empty;

        public string TimestampText
            => Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        public double DurationMilliseconds => Duration?.TotalMilliseconds ?? 0;

        public string CopyText
        {
            get
            {
                var builder = new StringBuilder();
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} - {1}",
                    Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Title));

                if (Duration.HasValue)
                    builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "Duration: {0}",
                        FormatDuration(Duration.Value)));

                if (string.IsNullOrWhiteSpace(Message) == false)
                    builder.AppendLine(Message);

                return builder.ToString().TrimEnd();
            }
        }

        public string SummaryLabel
        {
            get
            {
                if (Title.Length <= SUMMARY_LABEL_LENGTH)
                    return Title;

                return string.Format(CultureInfo.InvariantCulture,
                    "{0}…",
                    Title.Substring(0, SUMMARY_LABEL_LENGTH - 1));
            }
        }

        public IBrush BackgroundBrush => Level switch
        {
            LogLevel.Success => SuccessBackgroundBrush,
            LogLevel.Warning => WarningBackgroundBrush,
            LogLevel.Error => ErrorBackgroundBrush,
            _ => InfoBackgroundBrush
        };

        public IBrush BorderBrush => Level switch
        {
            LogLevel.Success => SuccessBorderBrush,
            LogLevel.Warning => WarningBorderBrush,
            LogLevel.Error => ErrorBorderBrush,
            _ => InfoBorderBrush
        };

        public IBrush AccentBrush => Level switch
        {
            LogLevel.Success => SuccessAccentBrush,
            LogLevel.Warning => WarningAccentBrush,
            LogLevel.Error => ErrorAccentBrush,
            _ => InfoAccentBrush
        };

        public string ToggleButtonLabel => _isMessageExpanded ? "Collapse" : "Expand";

        public bool ShowToggleButton => _hasExpandableMessage;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void ToggleMessageExpansion()
        {
            if (_hasExpandableMessage == false)
                return;

            _isMessageExpanded = !_isMessageExpanded;

            NotifyPropertyChanged(nameof(DisplayMessage));
            NotifyPropertyChanged(nameof(ToggleButtonLabel));
        }

        static string NormalizeLineEndings(string message)
            => message
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

        static string BuildCollapsedMessage(IReadOnlyList<string> lines)
        {
            var builder = new StringBuilder();

            for (var index = 0; index < COLLAPSED_MESSAGE_MAX_LINES; index++)
            {
                if (index > 0)
                    builder.AppendLine();

                builder.Append(lines[index]);
            }

            builder.AppendLine();
            builder.Append(COLLAPSED_SUFFIX);

            return builder.ToString();
        }

        void NotifyPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        readonly bool _hasExpandableMessage;
        readonly string _collapsedMessage = string.Empty;
        bool _isMessageExpanded;
    }

    sealed class LogSummaryEntry
    {
        const double MIN_WIDTH = 6;

        public LogSummaryEntry(LogDisplayEntry entry, double maxDuration, double maxWidth)
        {
            Title = entry.Title;
            FullTitle = entry.Title;
            DurationDisplay = entry.DurationDisplay;
            AccentBrush = entry.AccentBrush;
            GraphWidth = CalculateWidth(entry.DurationMilliseconds, maxDuration, maxWidth);
        }

        public string Title { get; }

        public string FullTitle { get; }

        public string DurationDisplay { get; }

        public IBrush AccentBrush { get; }

        public double GraphWidth { get; }

        public string CopyText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(DurationDisplay))
                    return Title;

                return string.Format(CultureInfo.InvariantCulture,
                    "{0} ({1})",
                    Title,
                    DurationDisplay);
            }
        }

        static double CalculateWidth(double duration, double maxDuration, double maxWidth)
        {
            if (maxWidth <= 0)
                maxWidth = DEFAULT_SUMMARY_BAR_WIDTH;

            if (duration <= 0 || maxDuration <= 0)
                return MIN_WIDTH;

            var ratio = duration / maxDuration;
            return Math.Max(MIN_WIDTH, ratio * maxWidth);
        }
    }
}
