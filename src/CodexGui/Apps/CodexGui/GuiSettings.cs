using System.Text.Json;

namespace CodexGui.Apps.CodexGui;

public class GuiSettings
{
    public string UnityPath { get; set; } = string.Empty;
    public string SetupSourceRepoUrl { get; set; } = string.Empty;
    public string SetupTargetRepoUrl { get; set; } = string.Empty;
    public string SetupSourceBranch { get; set; } = string.Empty;
    public string SetupTargetBranch { get; set; } = string.Empty;
    public string ExportSourceRepoUrl { get; set; } = string.Empty;
    public string ExportTargetRepoUrl { get; set; } = string.Empty;
    public string ExportSourceBranch { get; set; } = string.Empty;
    public string ExportTargetBranch { get; set; } = string.Empty;
    public string CsprojProjectPath { get; set; } = string.Empty;
    public bool CsprojRunUnity { get; set; } = true;
    public bool CsprojMakeCommits { get; set; } = true;
    public bool CsprojPushWhenDone { get; set; } = true;
    public string Pat { get; set; } = string.Empty;

    public static GuiSettings Load(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<GuiSettings>(json) ?? new GuiSettings();
            settings.Pat = PatObfuscator.Decode(settings.Pat);
            settings.CsprojProjectPath ??= string.Empty;
            if (json.Contains("CsprojRunUnity") == false)
                settings.CsprojRunUnity = true;
            if (json.Contains("CsprojMakeCommits") == false)
                settings.CsprojMakeCommits = true;
            if (json.Contains("CsprojPushWhenDone") == false)
                settings.CsprojPushWhenDone = true;
            return settings;
        }

        return new GuiSettings();
    }

    public void Save(string path)
    {
        var clone = new GuiSettings
        {
            UnityPath = UnityPath,
            SetupSourceRepoUrl = SetupSourceRepoUrl,
            SetupTargetRepoUrl = SetupTargetRepoUrl,
            SetupSourceBranch = SetupSourceBranch,
            SetupTargetBranch = SetupTargetBranch,
            ExportSourceRepoUrl = ExportSourceRepoUrl,
            ExportTargetRepoUrl = ExportTargetRepoUrl,
            ExportSourceBranch = ExportSourceBranch,
            ExportTargetBranch = ExportTargetBranch,
            Pat = PatObfuscator.Encode(Pat),
            CsprojProjectPath = CsprojProjectPath,
            CsprojRunUnity = CsprojRunUnity,
            CsprojMakeCommits = CsprojMakeCommits,
            CsprojPushWhenDone = CsprojPushWhenDone
        };

        var json = JsonSerializer.Serialize(clone);
        File.WriteAllText(path, json);
    }
}
