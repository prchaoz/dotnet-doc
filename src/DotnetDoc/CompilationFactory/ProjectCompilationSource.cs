using DotnetDoc.AssemblyResolution;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotnetDoc.CompilationFactory;

public class ProjectCompilationSource : ICompilationSource
{
    private readonly string _projectPath;
    private readonly string? _frameworkOverride;
    private static bool _msbuildRegistered;

    public ProjectCompilationSource(string projectPath, string? frameworkOverride)
    {
        _projectPath = ResolveProjectPath(projectPath);
        _frameworkOverride = frameworkOverride;
    }

    public async Task<CompilationResult> CreateCompilationAsync()
    {
        EnsureMSBuildRegistered();

        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine($"Workspace warning: {e.Diagnostic.Message}");
        });

        var project = await workspace.OpenProjectAsync(_projectPath);
        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException($"Could not create compilation for {_projectPath}");

        var sourceMap = BuildSourceMap(project, compilation);
        return new CompilationResult(compilation, sourceMap);
    }

    private Dictionary<string, SourceLabel> BuildSourceMap(
        Microsoft.CodeAnalysis.Project project, Compilation compilation)
    {
        var sourceMap = new Dictionary<string, SourceLabel>();
        var projectLabel = new SourceLabel(SourceKind.Project, _projectPath);

        // The project's own assembly
        var projectAsmName = compilation.AssemblyName;
        if (projectAsmName != null)
            sourceMap[projectAsmName] = projectLabel;

        // Metadata references — classify as runtime or package
        foreach (var reference in project.MetadataReferences)
        {
            if (reference is PortableExecutableReference peRef && peRef.FilePath != null)
            {
                var asmName = Path.GetFileNameWithoutExtension(peRef.FilePath);
                var label = ClassifyReference(peRef.FilePath);
                sourceMap.TryAdd(asmName, label);
            }
        }

        return sourceMap;
    }

    private static SourceLabel ClassifyReference(string dllPath)
    {
        var normalized = dllPath.Replace('\\', '/');

        // Check if it's from a NuGet package cache
        if (normalized.Contains("/.nuget/packages/") || normalized.Contains("/nuget/packages/"))
        {
            var packageInfo = ExtractPackageInfo(normalized);
            if (packageInfo != null)
                return new SourceLabel(SourceKind.Package, packageInfo);
        }

        // Check if it's from SDK packs
        if (normalized.Contains("/packs/"))
        {
            var runtimeInfo = ExtractRuntimeInfo(normalized);
            if (runtimeInfo != null)
                return new SourceLabel(SourceKind.Runtime, runtimeInfo);
        }

        // Default: treat as runtime
        return new SourceLabel(SourceKind.Runtime, "unknown");
    }

    private static string? ExtractPackageInfo(string path)
    {
        // Path: .../packages/packageid/version/lib/tfm/assembly.dll
        var parts = path.Replace('\\', '/').Split('/');
        var packagesIdx = Array.FindIndex(parts, p =>
            string.Equals(p, "packages", StringComparison.OrdinalIgnoreCase));

        if (packagesIdx >= 0 && packagesIdx + 2 < parts.Length)
            return $"{parts[packagesIdx + 1]}/{parts[packagesIdx + 2]}";

        return null;
    }

    private static string? ExtractRuntimeInfo(string path)
    {
        // Path: .../packs/Microsoft.NETCore.App.Ref/8.0.11/ref/net8.0/assembly.dll
        var parts = path.Replace('\\', '/').Split('/');
        var packsIdx = Array.FindIndex(parts, p =>
            string.Equals(p, "packs", StringComparison.OrdinalIgnoreCase));

        if (packsIdx >= 0 && packsIdx + 2 < parts.Length)
        {
            var packName = parts[packsIdx + 1];
            var version = parts[packsIdx + 2];

            // Strip ".Ref" suffix
            if (packName.EndsWith(".Ref"))
                packName = packName[..^4];

            var majorMinor = string.Join(".", version.Split('.').Take(2));
            return $"{packName}/{majorMinor}";
        }

        return null;
    }

    private static string ResolveProjectPath(string path)
    {
        if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(path);

        if (Directory.Exists(path))
        {
            var csprojFiles = Directory.GetFiles(path, "*.csproj");
            if (csprojFiles.Length == 1)
                return Path.GetFullPath(csprojFiles[0]);
            if (csprojFiles.Length > 1)
                throw new ArgumentException(
                    $"Multiple .csproj files found in '{path}'. Specify the exact .csproj file.");
            throw new FileNotFoundException($"No .csproj file found in '{path}'.");
        }

        throw new FileNotFoundException($"Project not found: '{path}'");
    }

    private static void EnsureMSBuildRegistered()
    {
        if (_msbuildRegistered) return;
        MSBuildLocator.RegisterDefaults();
        _msbuildRegistered = true;
    }
}
