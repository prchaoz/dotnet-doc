using DotnetDescribe.AssemblyResolution;
using Microsoft.CodeAnalysis;

namespace DotnetDescribe.CompilationFactory;

public record CompilationResult(
    Compilation Compilation,
    Dictionary<string, SourceLabel> AssemblySourceMap
);

public interface ICompilationSource
{
    Task<CompilationResult> CreateCompilationAsync();
}
