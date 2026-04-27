using DotnetDoc.AssemblyResolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotnetDoc.CompilationFactory;

public class RuntimeCompilationSource : ICompilationSource
{
    private readonly string _id;
    private readonly string _version;

    public RuntimeCompilationSource(string runtimeSpec)
    {
        (_id, _version) = RuntimeAssemblyResolver.ParseRuntimeSpec(runtimeSpec);
    }

    public Task<CompilationResult> CreateCompilationAsync()
    {
        var assemblies = RuntimeAssemblyResolver.Resolve(_id, _version);
        var sourceMap = new Dictionary<string, SourceLabel>();
        var references = new List<MetadataReference>();

        foreach (var asm in assemblies)
        {
            var docProvider = asm.XmlDocPath != null
                ? XmlDocumentationProvider.CreateFromFile(asm.XmlDocPath)
                : null;

            var reference = MetadataReference.CreateFromFile(asm.DllPath, documentation: docProvider);
            references.Add(reference);

            var asmName = Path.GetFileNameWithoutExtension(asm.DllPath);
            sourceMap[asmName] = asm.Source;
        }

        var compilation = CSharpCompilation.Create(
            "DocQuery",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return Task.FromResult(new CompilationResult(compilation, sourceMap));
    }
}
