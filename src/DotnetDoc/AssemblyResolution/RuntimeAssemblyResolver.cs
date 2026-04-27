using System.Runtime.InteropServices;

namespace DotnetDoc.AssemblyResolution;

public static class RuntimeAssemblyResolver
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["netcore"] = "Microsoft.NETCore.App",
        ["aspnet"] = "Microsoft.AspNetCore.App",
        ["desktop"] = "Microsoft.WindowsDesktop.App",
    };

    public static (string Id, string Version) ParseRuntimeSpec(string spec)
    {
        var slashIndex = spec.IndexOf('/');
        if (slashIndex < 0)
            throw new ArgumentException($"Invalid runtime spec '{spec}'. Expected format: id/version (e.g., netcore/8.0)");

        var id = spec[..slashIndex];
        var version = spec[(slashIndex + 1)..];

        if (Aliases.TryGetValue(id, out var fullName))
            id = fullName;

        return (id, version);
    }

    public static List<ResolvedAssembly> Resolve(string id, string version)
    {
        var packsDir = FindPacksDirectory();
        var refPackDir = Path.Combine(packsDir, $"{id}.Ref");

        if (!Directory.Exists(refPackDir))
            throw new DirectoryNotFoundException(
                $"Runtime ref pack not found: {refPackDir}\nAvailable packs: {string.Join(", ", ListAvailablePacks(packsDir))}");

        var versionDir = FindBestVersionDir(refPackDir, version);
        var tfm = $"net{version}";
        var refDir = Path.Combine(versionDir, "ref", tfm);

        if (!Directory.Exists(refDir))
        {
            // Try finding a matching ref subfolder
            var refParent = Path.Combine(versionDir, "ref");
            if (Directory.Exists(refParent))
            {
                var candidates = Directory.GetDirectories(refParent);
                refDir = candidates.FirstOrDefault(d => Path.GetFileName(d).StartsWith($"net{version}"))
                         ?? candidates.FirstOrDefault()
                         ?? refDir;
            }
        }

        if (!Directory.Exists(refDir))
            throw new DirectoryNotFoundException(
                $"Ref assemblies not found at: {refDir}");

        var sourceLabel = new SourceLabel(SourceKind.Runtime, $"{id}/{version}");
        return Directory.GetFiles(refDir, "*.dll")
            .Select(dll =>
            {
                var xmlPath = Path.ChangeExtension(dll, ".xml");
                return new ResolvedAssembly(dll, File.Exists(xmlPath) ? xmlPath : null, sourceLabel);
            })
            .ToList();
    }

    public static string FindPacksDirectory()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        // runtimeDir is like /usr/local/share/dotnet/shared/Microsoft.NETCore.App/8.0.x/
        // packs is at /usr/local/share/dotnet/packs/
        var dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
        var packsDir = Path.Combine(dotnetRoot, "packs");

        if (!Directory.Exists(packsDir))
            throw new DirectoryNotFoundException(
                $"Could not find SDK packs directory. Looked at: {packsDir}");

        return packsDir;
    }

    private static string FindBestVersionDir(string refPackDir, string requestedVersion)
    {
        if (!Directory.Exists(refPackDir))
            throw new DirectoryNotFoundException($"Pack directory not found: {refPackDir}");

        var versionDirs = Directory.GetDirectories(refPackDir)
            .Select(d => Path.GetFileName(d))
            .OrderByDescending(v => v)
            .ToList();

        // Exact match first
        var exact = versionDirs.FirstOrDefault(v => v == requestedVersion);
        if (exact != null)
            return Path.Combine(refPackDir, exact);

        // Prefix match (e.g., "8.0" matches "8.0.11")
        var prefixMatch = versionDirs.FirstOrDefault(v => v.StartsWith(requestedVersion));
        if (prefixMatch != null)
            return Path.Combine(refPackDir, prefixMatch);

        throw new DirectoryNotFoundException(
            $"Version '{requestedVersion}' not found in {refPackDir}. Available: {string.Join(", ", versionDirs)}");
    }

    private static IEnumerable<string> ListAvailablePacks(string packsDir)
    {
        return Directory.GetDirectories(packsDir)
            .Select(Path.GetFileName)
            .Where(n => n != null && n.EndsWith(".Ref"))
            .Select(n => n![..^4])!;
    }
}
