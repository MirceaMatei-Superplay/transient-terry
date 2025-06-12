# Usage

Run the tool by providing the root folder that contains the Unity generated `.csproj` files.
If no path is supplied the current directory is used.

```bash
dotnet run -- [path]
```

The program rewires Unity references and analyzer entries. It copies `Directory.Build.props` from
`Context/ExampleUnityProjectRoot` and creates a `UnityAssemblies` folder at the given root.
