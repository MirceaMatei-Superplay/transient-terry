using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Common.Scripts;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow : Window
{
    const string SETTINGS_FILE_NAME = "guiSettings.json";
    const int BOX_WIDTH = 400;
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
    Stopwatch? _activeOperationStopwatch;

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
}
