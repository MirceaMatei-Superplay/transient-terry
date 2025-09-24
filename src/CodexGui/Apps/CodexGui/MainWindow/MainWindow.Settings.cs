using Avalonia.Threading;
using Common.Scripts;
using System;
using System.Collections.Generic;
using System.IO;

namespace CodexGui.Apps.CodexGui;

public partial class MainWindow
{
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
            if (_settings.ExportSubmoduleSettings.TryGetValue(name, out var setting) == false)
                setting = _settings.ExportSubmoduleSettings[name] = new SubmoduleExportSettings();

            setting.IsCreatingNewBranch = check.IsChecked == true;
            if (setting.IsCreatingNewBranch)
            {
                setting.NewBranchName = _submoduleNewBranchBoxes.TryGetValue(name, out var branchBox)
                    ? branchBox.Text ?? string.Empty
                    : string.Empty;
                setting.BaseBranch = string.Empty;
            }
            else
            {
                setting.BaseBranch = _submoduleBranchBoxes.TryGetValue(name, out var branchBox)
                    && branchBox.SelectedItem != null
                    ? branchBox.SelectedItem.ToString() ?? string.Empty
                    : string.Empty;
                setting.NewBranchName = string.Empty;
            }
        }

        _settings.Save(_settingsFilePath);
    }

    static void EnsureSettingsDirectory()
    {
        if (Directory.Exists(_settingsFolder))
            return;

        Directory.CreateDirectory(_settingsFolder);
    }

    static string GetLegacySettingsPath()
        => Path.Combine(AppContext.BaseDirectory, SETTINGS_FILE_NAME);
}
