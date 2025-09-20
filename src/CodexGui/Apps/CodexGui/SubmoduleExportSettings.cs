namespace CodexGui.Apps.CodexGui;

public class SubmoduleExportSettings
{
    public string BaseBranch { get; set; } = string.Empty;
    public string NewBranchName { get; set; } = string.Empty;
    public bool IsCreatingNewBranch { get; set; }
        = false;
}
