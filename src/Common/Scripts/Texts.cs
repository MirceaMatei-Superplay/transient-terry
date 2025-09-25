namespace Common.Scripts
{
    public static class Texts
    {
        public const string BRANCH_EXISTS = "Branch {0} already exists. Please delete it before running the tool.";
        public const string CODEX_MAIN_BRANCH = "codex/main";
        public const string MAIN_BRANCH = "main";
        public const string DOT_GITMODULES = ".gitmodules";
        public const string FAILED_TO_START_PROCESS = "Failed to start process";
        public const string COMMAND_EXITED_TEMPLATE = "\nCommand exited with code {0}. Arguments: {1}\n";
        public const string OUTPUT_TEMPLATE = "Output: {0}\n";
        public const int UNITY_ALREADY_OPEN_EXIT_CODE = 1073741845;
        public const string KILLING_UNITY_PROCESS = "Unity project already open; killing running instance and retrying";
        public const string GIT = "git";
        public const string GITHUB_TOKEN_ENV = "GITHUB_TOKEN";
        public const string SETUP_FINISHED = "Rewiring tool finished";
        public const string GH_PAT_ENV = "GH_PAT";
        public const string CHECKING_OUT = "-C {0} checkout {1}";
        public const string SWITCH = "-C {0} switch {1}";
        public const string RUNNING_COMMAND = "Running command: {0} {1}";
        public const string CHECKING_OUT_STATUS = "Checking out {0} in {1}";
        public const string FETCHING_BRANCH_STATUS = "Branch {0} not found locally. Fetching from {1}.";
        public const string SWITCHING_OUT_STATUS = "Switching from {0} to {1}";
        public const string RESETTING_HARD = "-C {0} reset --hard {1}";
        public const string RESETTING_HARD_STATUS = "Resetting hard to {0} in {1}";
        
        public const string RUNTIME_FOLDER = "runtime";
        public const string SETUP_FOLDER = "Setup";
        public const string EXPORT_FOLDER = "Export";
        public const string TEMP_REWIRE_BRANCH_TEMPLATE = "codex/temp/rewire-{0}";
        public const string REMOVE_CODEX_FILES_DETAILS = "Remove codex required files and restore .gitignore settings for main development branch";
        public const string UPDATE_SUBMODULES_DETAILS = "Update submodules and restore shared meta files";
        public const string PREPARING_RUNTIME_FOLDER = "Preparing runtime folder";
        public const string RUNTIME_FOLDER_READY = "Runtime folder ready at {0}";
        public const string RESETTING_CACHED_REPOSITORY = "Resetting cached repository at {0}";
        public const string REUSING_CACHED_REPOSITORY = "Reusing cached repository at {0}";
        public const string CLEARING_CACHED_REPOSITORY = "Clearing cached repository at {0}";
        public const string BACKING_UP_FILES = "Backing up .gitignore and .gitmodules files";
        public const string TEMP_FOLDER = "temp";
        public const string DOT_GITIGNORE = ".gitignore";
        public const string SOLUTION_FILES_PATTERN = "*.sln";
        public const string DIRECTORY_BUILD_PROPS_FILE = "Directory.Build.props";
        public const string UNITY_ASSEMBLIES_FOLDER = "UnityAssemblies";
        public const string ASSETS_FOLDER = "Assets";
        public const string SHARED_FOLDER = "Shared";
        public const string SHARED_COPY_FOLDER = "Shared-copy";
        public const string SHARED_GITMODULES_TEMP = "Shared.gitmodules";
        public const string SHARED_COPY_MISSING_ERROR = "Shared-copy directory does not exist. Please ensure the Shared folder was copied correctly.";
        public const string MERGE_BASE_BRANCH_HEAD = "-C {0} merge-base {1} HEAD";
        public const string ORIGIN_REMOTE = "origin";
        public const string SOURCE_REMOTE = "source";
        public const int MERGE_BASE_FETCH_ATTEMPTS = 4;
        public const int MERGE_BASE_DEEPEN_INCREMENT = 32;
        public const string IMPORT_USAGE = "Usage: submodules-deflattener-import <repo-url> <target-branch> <source-branch>";
        public const string REWIRING_FOR_DEV_ENVIRONMENT = "Rewiring for dev environment";
        public const string PR_DESCRIPTION = "This PR rewires the project to be compatible with a local Unity dev environment.";
        public const string PR_SUBMODULE_CHANGES = "It includes the following submodule changes:\n";
        public const string PR_SUBMODULE_WARNING = "⚠️ Please merge the submodules first, then update the submodule head on this branch with the merge commit if it is not a fast forward commit and only then merge this PR.";
        public const string MAIN_PR_LINK_TEMPLATE = "Main PR: {0}";
        public const string GITHUB_CLIENT_PRODUCT = "codex-exporter";
        public const string SQUASH_COMMIT_MESSAGE = "Squashed commit from codex export";
        public const string SQUASH_MERGE_COMMIT_MESSAGE = "Squash merge temp branch";
        public const string SUBMODULE_PR_BODY_TEMPLATE = "Rewiring for dev environment\n{0}";
        public const string COMMIT_SUBJECT_DEFLATTEN_TEMPLATE = "Codex deflatten step {0}";
        public const string USAGE = "Usage: codex-exporter <source-repo-url> <target-repo-url> <source-branch> <target-branch>";
        public const string STARTING_REWIRING_TEMPLATE = "Starting rewiring for {0}";
        public const string REWIRING_FINISHED = "Rewiring tool finished";
        public const string REPOSITORY_NAME_RESOLVED = "Repository name resolved to {0}";
        public const string NEXT_PR_NUMBER = "Next PR number is {0}";
        public const string CREATING_AND_CHECKING_OUT = "Creating and checking out {0}";
        public const string CLONE_COMMAND = "clone --depth 1 {0} {1}";
        public const string CLONING_REPOSITORY = "Cloning repository from {0} to {1}";
        public const string LIST_REMOTE_BRANCHES = "ls-remote --heads {0}";
        public const string REFS_HEADS_PREFIX = "refs/heads/";
        public static string MERGE_FAILED_ERROR = "Merge failed. Please resolve conflicts and try again.";
    }
}
