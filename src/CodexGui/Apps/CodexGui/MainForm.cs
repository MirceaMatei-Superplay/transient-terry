using Common.Scripts;
using ExportTool = SubmodulesDeflattenerExport.Scripts.SubmodulesDeflattenerExport;
using SetupRunner = SubmodulesFlattenerSetup.Scripts.SubmodulesFlattenerSetup;
using CsprojSetupToolApp.Apps;
using System.Collections.Generic;
using System.Linq;

namespace CodexGui.Apps.CodexGui;

public class MainForm : Form
{
    const string SETTINGS_FILE = "guiSettings.json";

    const int BOX_WIDTH = 400;
    const int CHECK_BOX_WIDTH = 300;

    readonly TabControl _tabControl = new() { Dock = DockStyle.Fill };

    readonly TextBox _unityPathBox = new();
    readonly TextBox _setupSourceRepoUrlBox = new();
    readonly TextBox _setupTargetRepoUrlBox = new();
    readonly ComboBox _setupSourceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox _setupTargetBox = new();
    readonly TextBox _setupPatBox = new() { PasswordChar = '*' };
    readonly TextBox _exportSourceRepoUrlBox = new();
    readonly TextBox _exportTargetRepoUrlBox = new();
    readonly ComboBox _exportSourceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox _exportTargetBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox _exportPatBox = new() { PasswordChar = '*' };
    readonly TextBox _csprojProjectPathBox = new();
    readonly CheckBox _csprojRunUnityBox = new()
    {
        Text = "Run Unity",
        Checked = true,
        Width = CHECK_BOX_WIDTH
    };
    readonly CheckBox _csprojMakeCommitsBox = new()
    {
        Text = "Make commits",
        Checked = true,
        Width = CHECK_BOX_WIDTH
    };
    readonly CheckBox _csprojPushWhenDoneBox = new()
    {
        Text = "Push when done",
        Checked = true,
        Width = CHECK_BOX_WIDTH
    };

    readonly Button _runCsprojSetupButton = new()
    {
        Text = "Run CsprojSetup",
        Width = 150
    };

    readonly Button _runFlattenButton = new()
    {
        Text = "Run Flatten",
        Width = 150
    };

    readonly Button _runCsprojRelinkButton = new()
    {
        Text = "Run Csproj relink",
        Width = 150
    };

    readonly Button _runSetupButton = new()
    {
        Text = "Run Full Setup",
        Width = 150
    };
    readonly Button _runExportButton = new()
    {
        Text = "Run Export",
        Width = 150,
        Enabled = false
    };
    readonly Button _checkDiffsButton = new()
    {
        Text = "Check diffs",
        Width = 150
    };
    readonly FlowLayoutPanel _submodulePanel = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown };
    readonly Dictionary<string, ComboBox> _submoduleBranchBoxes = new();
    readonly Dictionary<string, CheckBox> _submoduleMakeBranchChecks = new();
    readonly Dictionary<string, TextBox> _submoduleNewBranchBoxes = new();
    List<SubmoduleInfo> _diffSubmodules = new();

    bool _isInternalChange;
    bool _isLoading;

    GuiSettings _settings = new();

    public MainForm()
    {
        Text = "Transient Terry";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ClientSize = new Size(800, 700);
        MinimumSize = new Size(600, 500);

        var setupLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var exportLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var regenLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

        var setupTab = new TabPage("Remote Setup") { AutoScroll = true };
        var exportTab = new TabPage("Export") { AutoScroll = true };
        var regenTab = new TabPage("Local Setup") { AutoScroll = true };

        setupTab.Controls.Add(setupLayout);
        exportTab.Controls.Add(exportLayout);
        regenTab.Controls.Add(regenLayout);

        _tabControl.TabPages.Add(setupTab);
        _tabControl.TabPages.Add(exportTab);
        _tabControl.TabPages.Add(regenTab);
        Controls.Add(_tabControl);

        AddRow(setupLayout, "Source Repo URL", _setupSourceRepoUrlBox, null, BOX_WIDTH);
        AddRow(setupLayout, "Target Repo URL", _setupTargetRepoUrlBox, null, BOX_WIDTH);
        AddPathRow(setupLayout, "Unity Path", _unityPathBox, (_, _) => BrowseForUnityPath(_unityPathBox));
        AddRow(setupLayout, "Setup Source Branch", _setupSourceBox, async (_, _) => await UpdateSetupBranches());
        AddRow(setupLayout, "Setup Target Branch", _setupTargetBox);
        AddRow(setupLayout, "GH_PAT", _setupPatBox);
        setupLayout.Controls.Add(_runSetupButton);

        AddRow(exportLayout, "Source Repo URL", _exportSourceRepoUrlBox, null, BOX_WIDTH);
        AddRow(exportLayout, "Target Repo URL", _exportTargetRepoUrlBox, null, BOX_WIDTH);
        AddRow(exportLayout, "Export Source Branch", _exportSourceBox, async (_, _) => await UpdateExportBranches());
        AddRow(exportLayout, "Export Target Branch", _exportTargetBox, async (_, _) => await UpdateExportBranches());
        AddRow(exportLayout, "GH_PAT", _exportPatBox);
        exportLayout.Controls.Add(_checkDiffsButton);
        exportLayout.Controls.Add(_submodulePanel);
        exportLayout.Controls.Add(_runExportButton);

        AddPathRow(
            regenLayout,
            "Project Path",
            _csprojProjectPathBox,
            (_, _) => BrowseForFolder(_csprojProjectPathBox));
        AddCheckRow(regenLayout, _csprojRunUnityBox);
        AddCheckRow(regenLayout, _csprojMakeCommitsBox);
        AddCheckRow(regenLayout, _csprojPushWhenDoneBox);
        regenLayout.Controls.Add(_runCsprojSetupButton);
        regenLayout.Controls.Add(_runFlattenButton);
        regenLayout.Controls.Add(_runCsprojRelinkButton);

        _runSetupButton.Click += async (_, _) => await RunSetup();
        _runExportButton.Click += async (_, _) => await RunExport();
        _checkDiffsButton.Click += async (_, _) => await CheckDiffs();
        _runCsprojSetupButton.Click += async (_, _) => await RunCsprojSetup();
        _runFlattenButton.Click += async (_, _) => await RunFlatten();
        _runCsprojRelinkButton.Click += async (_, _) => await RunCsprojRelink();

        RegisterEvents();
        LoadSettings();
    }

    void AddRow(TableLayoutPanel panel, string label, Control box,
        EventHandler? refreshHandler = null, int width = BOX_WIDTH)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        box.Width = width;
        panel.Controls.Add(box);

        if (refreshHandler == null)
        {
            panel.Controls.Add(box);
            return;
        }

        var inner = new FlowLayoutPanel { AutoSize = true };
        inner.Controls.Add(box);
        var refresh = new Button { Text = "Refresh ⭮" };
        refresh.Click += refreshHandler;
        inner.Controls.Add(refresh);
        panel.Controls.Add(inner);
    }

    void AddPathRow(TableLayoutPanel panel, string label, TextBox box, EventHandler browseHandler)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        var inner = new FlowLayoutPanel { AutoSize = true };
        box.Width = BOX_WIDTH;
        inner.Controls.Add(box);
        var browse = new Button { Text = "Browse..." };
        browse.Click += browseHandler;
        inner.Controls.Add(browse);
        panel.Controls.Add(inner);

        EnableDragAndDrop(box);
    }

    void AddCheckRow(TableLayoutPanel panel, CheckBox box)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(box);
    }

    void LoadSettings()
    {
        _isLoading = true;

        _settings = GuiSettings.Load(SETTINGS_FILE);
        _unityPathBox.Text = _settings.UnityPath;
        _setupSourceRepoUrlBox.Text = _settings.SetupSourceRepoUrl;
        _setupTargetRepoUrlBox.Text = _settings.SetupTargetRepoUrl;
        _setupSourceBox.Text = _settings.SetupSourceBranch;
        _setupTargetBox.Text = _settings.SetupTargetBranch;
        _exportSourceRepoUrlBox.Text = _settings.ExportSourceRepoUrl;
        _exportTargetRepoUrlBox.Text = _settings.ExportTargetRepoUrl;
        _exportSourceBox.Text = _settings.ExportSourceBranch;
        _exportTargetBox.Text = _settings.ExportTargetBranch;
        _setupPatBox.Text = _settings.Pat;
        _exportPatBox.Text = _settings.Pat;
        _csprojProjectPathBox.Text = _settings.CsprojProjectPath;
        _csprojRunUnityBox.Checked = _settings.CsprojRunUnity;
        _csprojMakeCommitsBox.Checked = _settings.CsprojMakeCommits;
        _csprojPushWhenDoneBox.Checked = _settings.CsprojPushWhenDone;

        _isLoading = false;
    }

    void SaveSettings()
    {
        if (_isLoading)
            return;

        _settings.UnityPath = _unityPathBox.Text;
        _settings.SetupSourceRepoUrl = _setupSourceRepoUrlBox.Text;
        _settings.SetupTargetRepoUrl = _setupTargetRepoUrlBox.Text;
        _settings.SetupSourceBranch = _setupSourceBox.Text;
        _settings.SetupTargetBranch = _setupTargetBox.Text;
        _settings.ExportSourceRepoUrl = _exportSourceRepoUrlBox.Text;
        _settings.ExportTargetRepoUrl = _exportTargetRepoUrlBox.Text;
        _settings.ExportSourceBranch = _exportSourceBox.Text;
        _settings.ExportTargetBranch = _exportTargetBox.Text;
        _settings.Pat = _setupPatBox.Text;
        _settings.CsprojProjectPath = _csprojProjectPathBox.Text;
        _settings.CsprojRunUnity = _csprojRunUnityBox.Checked;
        _settings.CsprojMakeCommits = _csprojMakeCommitsBox.Checked;
        _settings.CsprojPushWhenDone = _csprojPushWhenDoneBox.Checked;

        _settings.Save(SETTINGS_FILE);
    }

    void RegisterEvents()
    {
        _unityPathBox.TextChanged += OnValueChanged;
        _setupSourceRepoUrlBox.TextChanged += OnRepoUrlChanged;
        _setupTargetRepoUrlBox.TextChanged += OnRepoUrlChanged;
        _setupSourceBox.TextChanged += OnValueChanged;
        _setupTargetBox.TextChanged += OnValueChanged;
        _exportSourceRepoUrlBox.TextChanged += OnRepoUrlChanged;
        _exportTargetRepoUrlBox.TextChanged += OnRepoUrlChanged;
        _exportSourceBox.SelectedIndexChanged += OnValueChanged;
        _exportTargetBox.SelectedIndexChanged += OnValueChanged;

        _setupPatBox.TextChanged += OnPatChanged;
        _exportPatBox.TextChanged += OnPatChanged;
        _csprojProjectPathBox.TextChanged += OnValueChanged;
        _csprojRunUnityBox.CheckedChanged += OnValueChanged;
        _csprojMakeCommitsBox.CheckedChanged += OnValueChanged;
        _csprojPushWhenDoneBox.CheckedChanged += OnValueChanged;
    }

    void OnValueChanged(object? sender, EventArgs e)
    {
        if (_isInternalChange)
            return;

        SaveSettings();
    }

    async void OnRepoUrlChanged(object? sender, EventArgs e)
    {
        if (_isInternalChange)
            return;

        SaveSettings();

        if (sender == _setupSourceRepoUrlBox || sender == _setupTargetRepoUrlBox)
            await UpdateSetupBranches();
        else
            await UpdateExportBranches();
    }

    async void OnPatChanged(object? sender, EventArgs e)
    {
        if (_isInternalChange)
            return;

        _isInternalChange = true;

        if (sender == _setupPatBox && _exportPatBox.Text != _setupPatBox.Text)
            _exportPatBox.Text = _setupPatBox.Text;
        else if (sender == _exportPatBox && _setupPatBox.Text != _exportPatBox.Text)
            _setupPatBox.Text = _exportPatBox.Text;

        _isInternalChange = false;

        await UpdateSetupBranches();
        await UpdateExportBranches();

        SaveSettings();
    }


    void BrowseForFolder(TextBox target)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = target.Text };
        if (dialog.ShowDialog() == DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    void BrowseForUnityPath(TextBox target)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true
        };

        if (File.Exists(target.Text))
            fileDialog.InitialDirectory = Path.GetDirectoryName(target.Text);
        else if (Directory.Exists(target.Text))
            fileDialog.InitialDirectory = target.Text;

        if (fileDialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = fileDialog.FileName;
            return;
        }

        BrowseForFolder(target);
    }


    void EnableDragAndDrop(TextBox box)
    {
        box.AllowDrop = true;
        box.DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        };

        box.DragDrop += (_, e) =>
        {
            var paths = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (paths is { Length: > 0 })
                box.Text = paths[0];
        };
    }

    async Task RunSetup()
    {
        SaveSettings();
        var setup = new SetupRunner(
            _settings.UnityPath,
            _settings.SetupSourceRepoUrl,
            _settings.SetupTargetRepoUrl,
            _settings.SetupSourceBranch,
            _settings.SetupTargetBranch,
            _settings.Pat);
        await setup.Run();
        MessageBox.Show("Setup finished");
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
            if (check.Checked)
                newBranches[name] = _submoduleNewBranchBoxes[name].Text;

        var exporter = new ExportTool(_settings.Pat,
            _settings.ExportSourceRepoUrl, _settings.ExportTargetRepoUrl,
            _settings.ExportSourceBranch, _settings.ExportTargetBranch, baseRefs,
            newBranches);
        var mainPrUrl = await exporter.Run();
        var linkText = string.Format(Texts.MAIN_PR_LINK_TEMPLATE, mainPrUrl);
        using var form = new SuccessForm("Export finished", linkText, mainPrUrl);
        form.ShowDialog(this);
    }

    async Task RunCsprojSetup()
    {
        SaveSettings();
        await CsprojSetupToolApp.Apps.CsprojSetupToolApp.Run(
            _settings.UnityPath,
            _settings.CsprojProjectPath,
            _settings.CsprojRunUnity,
            _settings.CsprojMakeCommits,
            _settings.CsprojPushWhenDone);
        MessageBox.Show("CsprojSetup finished");
    }

    async Task RunFlatten()
    {
        SaveSettings();

        var runtime = Helpers.PrepareRuntime();
        var repoName = RepoUtils.GetRepoName(_settings.SetupTargetRepoUrl);
        var repoPath = Path.Combine(runtime, repoName);

        await CloneRepository(_settings.SetupTargetRepoUrl, repoPath);
        var tempBranch = $"temp-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await Helpers.CreateAndCheckoutBranch(repoPath, tempBranch, _settings.Pat, true);
        await FetchBranch(repoPath, _settings.SetupSourceRepoUrl, _settings.SetupSourceBranch);

        var flattener = new SubmodulesFlattener(_settings.Pat);
        await flattener.Run(repoPath, _settings.SetupSourceBranch, _settings.SetupTargetBranch);

        _settings.CsprojProjectPath = repoPath;
        _csprojProjectPathBox.Text = repoPath;
        SaveSettings();

        MessageBox.Show("Flatten finished; regenerate project files in Unity then run Csproj relink");
    }


    async Task RunCsprojRelink()
    {
        SaveSettings();
        await CsprojSetupToolApp.Apps.CsprojSetupToolApp.Run(
            _settings.UnityPath,
            _settings.CsprojProjectPath,
            false,
            _settings.CsprojMakeCommits,
            _settings.CsprojPushWhenDone);
        MessageBox.Show("Csproj relink finished");
    }

    async Task UpdateSetupBranches()
    {
        _isInternalChange = true;

        _setupSourceBox.Items.Clear();

        if (string.IsNullOrEmpty(_setupSourceRepoUrlBox.Text) == false)
        {
            var sourceBranches = await Helpers.GetRemoteBranches(_setupSourceRepoUrlBox.Text,
                _setupPatBox.Text);
            foreach (var branch in sourceBranches)
                _setupSourceBox.Items.Add(branch);
        }

        _setupSourceBox.Text = _settings.SetupSourceBranch;
        _setupTargetBox.Text = _settings.SetupTargetBranch;
        _isInternalChange = false;
    }

    async Task UpdateExportBranches()
    {
        _isInternalChange = true;

        _exportSourceBox.Items.Clear();
        _exportTargetBox.Items.Clear();

        if (string.IsNullOrEmpty(_exportSourceRepoUrlBox.Text) == false)
        {
            var sourceBranches = await Helpers.GetRemoteBranches(_exportSourceRepoUrlBox.Text,
                _exportPatBox.Text);
            foreach (var branch in sourceBranches)
                _exportSourceBox.Items.Add(branch);
        }

        if (string.IsNullOrEmpty(_exportTargetRepoUrlBox.Text) == false)
        {
            var targetBranches = await Helpers.GetRemoteBranches(_exportTargetRepoUrlBox.Text,
                _exportPatBox.Text);
            foreach (var branch in targetBranches)
                _exportTargetBox.Items.Add(branch);
        }

        _exportSourceBox.SelectedItem = _settings.ExportSourceBranch;
        _exportTargetBox.SelectedItem = _settings.ExportTargetBranch;
        _isInternalChange = false;
    }

    async Task CloneRepository(string url, string path)
    {
        Logger.Write(string.Format(Texts.CLONING_REPOSITORY, url, path));
        await Helpers.RunGit(string.Format(Texts.CLONE_COMMAND, url, path), _settings.Pat);
    }

    async Task FetchBranch(string path, string url, string branch)
    {
        Logger.Write(string.Format("Fetching {0} from {1}", branch, url));
        await Helpers.RunGit($"-C {path} fetch {url} {branch}:{branch}", _settings.Pat);
    }

    async Task CheckDiffs()
    {
        _checkDiffsButton.Enabled = false;
        _runExportButton.Enabled = false;
        _submodulePanel.Controls.Clear();
        _submoduleBranchBoxes.Clear();
        _submoduleMakeBranchChecks.Clear();
        _submoduleNewBranchBoxes.Clear();

        var tool = new ExportTool(
            _exportPatBox.Text,
            _exportSourceRepoUrlBox.Text,
            _exportTargetRepoUrlBox.Text,
            _exportSourceBox.Text,
            _exportTargetBox.Text);

        _diffSubmodules = await tool.GetChangedSubmodules();

        foreach (var submodule in _diffSubmodules)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            var branches = await Helpers.GetRemoteBranches(submodule.Url, _exportPatBox.Text);
            foreach (var branch in branches)
                combo.Items.Add(branch);
            combo.SelectedIndexChanged += (_, _) => SetRunExportEnabled();
            _submoduleBranchBoxes[submodule.Name] = combo;

            var makeBranch = new CheckBox { Text = "Make new branch" };
            var branchBox = new TextBox { Width = 150, Visible = false };
            makeBranch.CheckedChanged += (_, _) =>
            {
                combo.Visible = makeBranch.Checked == false;
                branchBox.Visible = makeBranch.Checked;
                if (makeBranch.Checked)
                    branchBox.Text = branches.Contains(_exportTargetBox.Text) ? string.Empty : _exportTargetBox.Text;
                SetRunExportEnabled();
            };
            branchBox.TextChanged += (_, _) => SetRunExportEnabled();
            _submoduleMakeBranchChecks[submodule.Name] = makeBranch;
            _submoduleNewBranchBoxes[submodule.Name] = branchBox;

            var row = new FlowLayoutPanel { AutoSize = true };
            row.Controls.Add(new Label { Text = $"{submodule.Name} base branch", AutoSize = true });
            row.Controls.Add(combo);
            row.Controls.Add(makeBranch);
            row.Controls.Add(branchBox);
            _submodulePanel.Controls.Add(row);
        }

        SetRunExportEnabled();
        _checkDiffsButton.Enabled = true;

        string message;
        if (_diffSubmodules.Count > 0)
        {
            var names = string.Join(", ", _diffSubmodules.Select(s => s.Name));
            message = $"Found diffs with the following submodules: ({names}) " +
                      "please pick a base branch for each";
        }
        else
        {
            message = "There are no submodule diffs, you may proceed with the Export";
        }

        MessageBox.Show(message);
    }

    void SetRunExportEnabled()
    {
        if (_submoduleBranchBoxes.Count == 0)
        {
            _runExportButton.Enabled = true;
            return;
        }

        foreach (var submodule in _diffSubmodules)
        {
            var makeNew = _submoduleMakeBranchChecks[submodule.Name].Checked;
            if (makeNew)
            {
                if (string.IsNullOrWhiteSpace(_submoduleNewBranchBoxes[submodule.Name].Text))
                {
                    _runExportButton.Enabled = false;
                    return;
                }
            }
            else if (_submoduleBranchBoxes[submodule.Name].SelectedItem == null)
            {
                _runExportButton.Enabled = false;
                return;
            }
        }

        _runExportButton.Enabled = true;
    }
}
