namespace DotnetDescribe.AssemblyResolution;

public enum SourceKind
{
    Runtime,
    Package,
    Project
}

public record SourceLabel(SourceKind Kind, string Identifier);

public record ResolvedAssembly(string DllPath, string? XmlDocPath, SourceLabel Source);
