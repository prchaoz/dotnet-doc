using DotnetDescribe.AssemblyResolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotnetDescribe.CompilationFactory;

public class PackageCompilationSource : ICompilationSource
{
    private readonly string _packageId;
    private readonly string _version;
    private readonly string? _frameworkOverride;

    public PackageCompilationSource(string packageSpec, string? frameworkOverride)
    {
        (_packageId, _version) = PackageAssemblyResolver.ParsePackageSpec(packageSpec);
        _frameworkOverride = frameworkOverride;
    }

    public Task<CompilationResult> CreateCompilationAsync()
    {
        var packageAssemblies = PackageAssemblyResolver.Resolve(_packageId, _version, _frameworkOverride);

        // Also load runtime ref assemblies so Roslyn can resolve base types
        var bestTfm = PackageAssemblyResolver.DetectBestTfm(_packageId, _version);
        var runtimeVersion = ExtractVersionFromTfm(bestTfm);
        List<ResolvedAssembly> runtimeAssemblies;
        try
        {
            runtimeAssemblies = RuntimeAssemblyResolver.Resolve("Microsoft.NETCore.App", runtimeVersion);
        }
        catch
        {
            // Fallback: try to find any available runtime
            runtimeAssemblies = TryFindAnyRuntime();
        }

        var sourceMap = new Dictionary<string, SourceLabel>();
        var references = new List<MetadataReference>();

        foreach (var asm in packageAssemblies.Concat(runtimeAssemblies))
        {
            var docProvider = asm.XmlDocPath != null
                ? XmlDocumentationProvider.CreateFromFile(asm.XmlDocPath)
                : null;

            var reference = MetadataReference.CreateFromFile(asm.DllPath, documentation: docProvider);
            references.Add(reference);

            var asmName = Path.GetFileNameWithoutExtension(asm.DllPath);
            sourceMap.TryAdd(asmName, asm.Source);
        }

        var compilation = CSharpCompilation.Create(
            "DocQuery",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return Task.FromResult(new CompilationResult(compilation, sourceMap));
    }

    private static string ExtractVersionFromTfm(string tfm)
    {
        // "net8.0" → "8.0", "netstandard2.0" → "8.0" (fallback)
        if (tfm.StartsWith("net") && !tfm.StartsWith("netstandard") && !tfm.StartsWith("netcoreapp"))
            return tfm[3..];

        if (tfm.StartsWith("netcoreapp"))
            return tfm[10..];

        return "8.0"; // fallback for netstandard
    }

    private static List<ResolvedAssembly> TryFindAnyRuntime()
    {
        var packsDir = RuntimeAssemblyResolver.FindPacksDirectory();
        var netCoreRefDir = Path.Combine(packsDir, "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(netCoreRefDir))
            return [];

        var latestVersion = Directory.GetDirectories(netCoreRefDir)
            .Select(Path.GetFileName)
            .OrderByDescending(v => v)
            .FirstOrDefault();

        if (latestVersion == null)
            return [];

        var majorMinor = string.Join(".", latestVersion.Split('.').Take(2));
        return RuntimeAssemblyResolver.Resolve("Microsoft.NETCore.App", majorMinor);
    }
}
