# Transient Terry

Transient Terry collects small command line utilities used to ready Unity-based repositories for the Codex AI pipeline. These tools rewrite project files to remove local Unity dependencies, flatten Git submodules and later restore them, or create pull requests for the produced changes.

## Projects

- **CsprojSetupToolApp** – scans Unity generated `.csproj` files, copies required assemblies into a local `UnityAssemblies` directory and rewrites the hint paths.
- **SubmodulesFlattener** – creates a branch where each submodule is replaced by its tracked files.
- **SubmodulesFlattenerSetup** – runs the flattener and then `CsprojSetupTool` to produce a Codex ready branch in a single step.
- **SubmodulesDeflattenerExport** – restores submodules in a deflattened repository and opens pull requests for the updates.
- **SubmodulesDeflattenerImport** – applies external submodule and main repo updates back to the Codex branch. It is a merger.
- **CodexGui** – a small Windows Forms wrapper that drives the exporter and setup tools.

All projects share common build settings from `Directory.Build.props` and live inside `transient-terry.sln`. See [USAGE.md](USAGE.md) for basic invocation examples.

## Rewiring Unity projects

When Unity generates project files it references the installation directly. `CsprojSetupToolApp` replaces these references so the project builds without Unity installed. The tool scans each `.csproj` for paths under `Unity/Hub/Editor` or `Library/ScriptAssemblies`, copies those assemblies into `UnityAssemblies` and updates the hint paths.

Example before:

```xml
<Reference Include="UnityEngine">
  <HintPath>C:\Program Files\Unity\Hub\Editor\6000.0.26f1\Editor\Data\Managed\UnityEngine\UnityEngine.dll</HintPath>
</Reference>
```

After running the tool:

```xml
<Reference Include="UnityEngine">
  <HintPath>UnityAssemblies\Managed\UnityEngine\UnityEngine.dll</HintPath>
</Reference>
```

Unity's default assemblies such as `UnityEngine.CoreModule` and `UnityEngine.UI` are copied automatically. A sample `Directory.Build.props` resides under `Context/ExampleUnityProjectRoot` and is written to the repository root during setup.
