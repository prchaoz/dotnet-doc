using DotnetDoc.AssemblyResolution;
using Microsoft.CodeAnalysis;

namespace DotnetDoc.CompilationFactory;

public record CompilationResult(
    Compilation Compilation,
    Dictionary<string, SourceLabel> AssemblySourceMap
);

public interface ICompilationSource
{
    Task<CompilationResult> CreateCompilationAsync();
}
