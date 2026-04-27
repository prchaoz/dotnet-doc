namespace DotnetDoc.AssemblyResolution;

public static class PackageAssemblyResolver
{
    private static readonly string[] TfmPriority =
    [
        "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
        "netstandard2.1", "netstandard2.0", "netstandard1.6",
        "netcoreapp3.1", "net472", "net471", "net47", "net462", "net461", "net46", "net45"
    ];

    public static (string Id, string Version) ParsePackageSpec(string spec)
    {
        var slashIndex = spec.LastIndexOf('/');
        if (slashIndex < 0)
            throw new ArgumentException($"Invalid package spec '{spec}'. Expected format: PackageId/Version (e.g., Newtonsoft.Json/13.0.3)");

        var id = spec[..slashIndex];
        var version = spec[(slashIndex + 1)..];
        return (id, version);
    }

    public static List<ResolvedAssembly> Resolve(string packageId, string version, string? frameworkOverride)
    {
        var nugetDir = FindNuGetCacheDir();
        var packageDir = Path.Combine(nugetDir, packageId.ToLowerInvariant(), version);

        if (!Directory.Exists(packageDir))
            throw new DirectoryNotFoundException(
                $"Package not found in NuGet cache: {packageDir}\n" +
                $"Try running: dotnet add package {packageId} --version {version}");

        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
        {
            // Some packages have ref/ instead of lib/
            libDir = Path.Combine(packageDir, "ref");
            if (!Directory.Exists(libDir))
                throw new DirectoryNotFoundException(
                    $"No lib/ or ref/ directory found in package: {packageDir}");
        }

        var tfmDir = SelectTfmDirectory(libDir, frameworkOverride);
        var sourceLabel = new SourceLabel(SourceKind.Package, $"{packageId}/{version}");

        return Directory.GetFiles(tfmDir, "*.dll")
            .Select(dll =>
            {
                var xmlPath = Path.ChangeExtension(dll, ".xml");
                return new ResolvedAssembly(dll, File.Exists(xmlPath) ? xmlPath : null, sourceLabel);
            })
            .ToList();
    }

    public static string DetectBestTfm(string packageId, string version)
    {
        var nugetDir = FindNuGetCacheDir();
        var libDir = Path.Combine(nugetDir, packageId.ToLowerInvariant(), version, "lib");
        if (!Directory.Exists(libDir))
            libDir = Path.Combine(nugetDir, packageId.ToLowerInvariant(), version, "ref");
        if (!Directory.Exists(libDir))
            return "net8.0";

        var available = Directory.GetDirectories(libDir).Select(Path.GetFileName).ToList();
        foreach (var tfm in TfmPriority)
        {
            if (available.Contains(tfm, StringComparer.OrdinalIgnoreCase))
                return tfm;
        }

        return available.FirstOrDefault() ?? "net8.0";
    }

    private static string SelectTfmDirectory(string libDir, string? frameworkOverride)
    {
        var available = Directory.GetDirectories(libDir)
            .Select(d => (Path: d, Name: Path.GetFileName(d)))
            .ToList();

        if (available.Count == 0)
            throw new DirectoryNotFoundException($"No TFM directories found in: {libDir}");

        if (frameworkOverride != null)
        {
            var match = available.FirstOrDefault(a =>
                string.Equals(a.Name, frameworkOverride, StringComparison.OrdinalIgnoreCase));
            if (match.Path != null)
                return match.Path;

            throw new DirectoryNotFoundException(
                $"Framework '{frameworkOverride}' not found. Available: {string.Join(", ", available.Select(a => a.Name))}");
        }

        // Pick highest priority TFM
        foreach (var tfm in TfmPriority)
        {
            var match = available.FirstOrDefault(a =>
                string.Equals(a.Name, tfm, StringComparison.OrdinalIgnoreCase));
            if (match.Path != null)
                return match.Path;
        }

        // Fall back to first available
        return available[0].Path;
    }

    private static string FindNuGetCacheDir()
    {
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackages) && Directory.Exists(nugetPackages))
            return nugetPackages;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDir = Path.Combine(home, ".nuget", "packages");

        if (Directory.Exists(defaultDir))
            return defaultDir;

        throw new DirectoryNotFoundException(
            "Could not find NuGet packages cache. Set NUGET_PACKAGES environment variable.");
    }
}
