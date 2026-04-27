using DotnetDoc.CompilationFactory;
using DotnetDoc.TypeResolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotnetDoc.Tests;

public class SymbolFinderTests
{
    private static Compilation CreateRuntimeCompilation()
    {
        // Find runtime ref assemblies
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var dotnetRoot = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
        var packsDir = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        var versionDir = Directory.GetDirectories(packsDir).OrderByDescending(d => d).First();
        var refDirs = Directory.GetDirectories(Path.Combine(versionDir, "ref"));
        var refDir = refDirs.OrderByDescending(d => d).First();

        var references = Directory.GetFiles(refDir, "*.dll")
            .Select(dll => MetadataReference.CreateFromFile(dll))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create("Test", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void Find_IDisposable_ReturnsOneMatch()
    {
        var compilation = CreateRuntimeCompilation();
        var queries = TypeQueryParser.Parse("IDisposable");
        var results = SymbolFinder.Find(compilation, queries);

        Assert.Single(results);
        Assert.Equal("System.IDisposable", results[0].Type.ToDisplayString());
        Assert.Null(results[0].Member);
    }

    [Fact]
    public void Find_FullyQualifiedType_Works()
    {
        var compilation = CreateRuntimeCompilation();
        var queries = TypeQueryParser.Parse("System.IO.Stream");
        var results = SymbolFinder.Find(compilation, queries);

        Assert.True(results.Count >= 1);
        Assert.Contains(results, r => r.Type.ToDisplayString() == "System.IO.Stream");
    }

    [Fact]
    public void Find_MemberQuery_ReturnsMember()
    {
        var compilation = CreateRuntimeCompilation();
        var queries = TypeQueryParser.Parse("Stream.Read");
        var results = SymbolFinder.Find(compilation, queries);

        Assert.True(results.Count >= 1);
        Assert.All(results, r =>
        {
            Assert.Equal("Stream", r.Type.Name);
            Assert.NotNull(r.Member);
            Assert.Equal("Read", r.Member!.Name);
        });
    }

    [Fact]
    public void Find_NonExistentType_ReturnsEmpty()
    {
        var compilation = CreateRuntimeCompilation();
        var queries = TypeQueryParser.Parse("ThisTypeDoesNotExist12345");
        var results = SymbolFinder.Find(compilation, queries);

        Assert.Empty(results);
    }

    [Fact]
    public void Find_GenericType_Works()
    {
        var compilation = CreateRuntimeCompilation();
        var queries = TypeQueryParser.Parse("List");
        var results = SymbolFinder.Find(compilation, queries);

        Assert.True(results.Count >= 1);
        Assert.Contains(results, r => r.Type.ToDisplayString().Contains("System.Collections.Generic.List"));
    }
}
