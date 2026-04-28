using DotnetDescribe.CompilationFactory;
using DotnetDescribe.Formatting;
using DotnetDescribe.TypeResolution;

namespace DotnetDescribe;

public record DocOptions(
    string? Runtime,
    string? Package,
    string? Project,
    string TypeOrMember,
    bool ShowAll,
    bool ShowPrivate,
    string? Framework,
    bool NoRefs
);

public static class DocCommand
{
    public static async Task<int> ExecuteAsync(DocOptions options)
    {
        try
        {
            var source = CreateCompilationSource(options);
            var compilationResult = await source.CreateCompilationAsync();
            var queries = TypeQueryParser.Parse(options.TypeOrMember);
            var results = SymbolFinder.Find(compilationResult.Compilation, queries);

            if (results.Count == 0)
            {
                Console.Error.WriteLine($"Type or member '{options.TypeOrMember}' not found.");
                return 1;
            }

            var isProjectMode = options.Project != null;
            var formatter = new SymbolOutputFormatter(
                compilationResult,
                options.ShowAll,
                options.ShowPrivate,
                showRefs: !options.NoRefs,
                isProjectMode);

            // Display all matching types and members
            var displayedTypes = new HashSet<string>();
            foreach (var result in results)
            {
                if (result.Member != null)
                {
                    Console.Write(formatter.FormatMember(result.Type, result.Member));
                }
                else
                {
                    var typeKey = result.Type.ToDisplayString();
                    if (displayedTypes.Add(typeKey))
                        Console.Write(formatter.FormatType(result.Type));
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static ICompilationSource CreateCompilationSource(DocOptions options)
    {
        var modeCount = (options.Runtime != null ? 1 : 0)
                      + (options.Package != null ? 1 : 0)
                      + (options.Project != null ? 1 : 0);

        if (modeCount == 0)
            throw new ArgumentException(
                "Specify one of --runtime, --package, or --project.");

        if (modeCount > 1)
            throw new ArgumentException(
                "Specify exactly one of --runtime, --package, or --project.");

        if (options.Runtime != null)
            return new RuntimeCompilationSource(options.Runtime);

        if (options.Package != null)
            return new PackageCompilationSource(options.Package, options.Framework);

        return new ProjectCompilationSource(options.Project!, options.Framework);
    }
}
