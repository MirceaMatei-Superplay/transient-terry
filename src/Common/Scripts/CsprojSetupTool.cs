using System.Xml.Linq;

namespace Common.Scripts
{
    public class CsprojSetupTool
    {
        readonly string _rootPath;
        readonly string _assembliesPath;
        readonly string _regenFileName;

        public CsprojSetupTool(string rootPath)
        {
            _rootPath = rootPath;
            _assembliesPath = Path.Combine(rootPath, "UnityAssemblies");
            _regenFileName = GetRegenerateScriptName();
        }

        public async Task Setup(bool makeCommits = true, bool pushWhenDone = true)
        {
            foreach (var path in Directory.GetFiles(_rootPath, "*.csproj", SearchOption.AllDirectories))
                ProcessProject(path);

            WriteDirectoryProps();
            if (makeCommits)
            {
                await Helpers.RunGit($"-C {_rootPath} add -A");
                await Helpers.RunGit($"-C {_rootPath} commit -m \"Csproj rewired\"");
                if (pushWhenDone)
                    await Helpers.RunGit($"-C {_rootPath} push --set-upstream origin HEAD");
            }
        }

        void ProcessProject(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var ns = doc.Root!.Name.Namespace;
            var hasChanged = false;

            foreach (var analyzer in doc.Descendants(ns + "Analyzer"))
            {
                var includeAttr = analyzer.Attribute("Include");
                if (includeAttr == null)
                    continue;

                var includePath = includeAttr.Value;
                if (IsUnityInstallPath(includePath))
                {
                    var dllName = GetFileName(includePath);
                    var dest = Path.Combine(_assembliesPath, "Analyzers", dllName);
                    CopyIfNeeded(includePath, dest);
                    includeAttr.Value = GetRelative(filePath, dest);
                    hasChanged = true;
                }
            }

            foreach (var reference in doc.Descendants(ns + "Reference"))
            {
                var hintElement = reference.Element(ns + "HintPath");
                if (hintElement == null)
                    continue;

                var hintPath = hintElement.Value;
                if (IsUnityInstallPath(hintPath))
                {
                    var dllName = GetFileName(hintPath);
                    var includeAttr = reference.Attribute("Include");
                    var include = includeAttr?.Value ?? Path.GetFileNameWithoutExtension(dllName);
                    var dest = Path.Combine(_assembliesPath, "Managed", include, dllName);
                    CopyIfNeeded(hintPath, dest);
                    hintElement.Value = GetRelative(filePath, dest);
                    hasChanged = true;
                }
                else if (IsProjectLibraryPath(hintPath))
                {
                    var dllName = GetFileName(hintPath);
                    var dest = Path.Combine(_assembliesPath, "Library", "ScriptAssemblies", dllName);
                    CopyIfNeeded(hintPath, dest);
                    hintElement.Value = GetRelative(filePath, dest);
                    hasChanged = true;
                }
            }

            foreach (var compile in doc.Descendants(ns + "Compile").ToList())
            {
                var includeAttr = compile.Attribute("Include");
                if (includeAttr == null)
                    continue;

                var includePath = includeAttr.Value.Replace('/', '\\');
                var targetPath = $"Assets\\Editor\\{_regenFileName}";
                if (string.Equals(includePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    compile.Remove();
                    hasChanged = true;
                }
            }

            if (hasChanged)
                doc.Save(filePath);
        }

        static string GetRegenerateScriptName()
        {
            var contextFolder = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "Context"));

            var file = Directory.GetFiles(contextFolder, "*.cs.txt").FirstOrDefault();
            return file == null ? "RegenerateProjectFiles.cs" : Path.GetFileNameWithoutExtension(file);
        }

        static bool IsUnityInstallPath(string path) =>
            path.Contains("Unity/Hub/Editor") || path.Contains("Unity\\Hub\\Editor");

        static bool IsProjectLibraryPath(string path) =>
            path.Contains("Library/ScriptAssemblies") || path.Contains("Library\\ScriptAssemblies");

        static void CopyIfNeeded(string source, string destination)
        {
            if (File.Exists(destination))
                return;

            var folder = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(folder);
            if (File.Exists(source))
                File.Copy(source, destination, true);
            else
                File.WriteAllText(destination, string.Empty);
        }
        
        static string GetFileName(string path)
        {
            var normalized = path.Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFileName(normalized);
        }

        static string GetRelative(string projectPath, string destination)
        {
            var projectDir = Path.GetDirectoryName(projectPath)!;
            var relative = Path.GetRelativePath(projectDir, destination);
            return relative.Replace(Path.DirectorySeparatorChar, '\\');
        }
        

        void WriteDirectoryProps()
        {
            var destination = Path.Combine(_rootPath, "Directory.Build.props");
            if (File.Exists(destination))
                return;

            var source = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "Context",
                    "ExampleUnityProjectRoot",
                    "Directory.Build.props"));

            File.Copy(source, destination, true);
        }

    }
}
