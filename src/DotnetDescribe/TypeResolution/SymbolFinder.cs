using Microsoft.CodeAnalysis;

namespace DotnetDescribe.TypeResolution;

public record FoundSymbol(INamedTypeSymbol Type, ISymbol? Member);

public static class SymbolFinder
{
    public static List<FoundSymbol> Find(Compilation compilation, List<TypeQuery> queries)
    {
        var allTypes = GetAllAccessibleTypes(compilation).ToList();

        foreach (var query in queries)
        {
            var results = FindForQuery(allTypes, query);
            if (results.Count > 0)
                return results;
        }

        return [];
    }

    private static List<FoundSymbol> FindForQuery(List<INamedTypeSymbol> allTypes, TypeQuery query)
    {
        var matchingTypes = FindTypes(allTypes, query.Namespace, query.TypeName);

        if (matchingTypes.Count == 0)
            return [];

        if (query.MemberName == null)
            return matchingTypes.Select(t => new FoundSymbol(t, null)).ToList();

        // Search for member in matched types
        var results = new List<FoundSymbol>();
        foreach (var type in matchingTypes)
        {
            var members = type.GetMembers(query.MemberName);
            if (members.Length > 0)
            {
                foreach (var member in members)
                    results.Add(new FoundSymbol(type, member));
            }
        }

        return results;
    }

    private static List<INamedTypeSymbol> FindTypes(List<INamedTypeSymbol> allTypes, string? ns, string typeName)
    {
        var results = new List<INamedTypeSymbol>();

        foreach (var type in allTypes)
        {
            var nameMatch = string.Equals(type.Name, typeName, StringComparison.Ordinal)
                         || string.Equals(type.MetadataName, typeName, StringComparison.Ordinal)
                         || MatchesDisplayName(type, typeName);

            if (!nameMatch)
                continue;

            if (ns != null)
            {
                var typeNs = type.ContainingNamespace?.ToDisplayString();
                if (!string.Equals(typeNs, ns, StringComparison.Ordinal))
                    continue;
            }

            results.Add(type);
        }

        return results;
    }

    private static bool MatchesDisplayName(INamedTypeSymbol type, string name)
    {
        // Handle generic types: "List" matches "List<T>"
        if (type.IsGenericType && type.Name == name)
            return true;

        // Handle fully qualified name match
        var fullName = type.ToDisplayString();
        return string.Equals(fullName, name, StringComparison.Ordinal);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllAccessibleTypes(Compilation compilation)
    {
        var seen = new HashSet<string>();

        // Types from source (project mode)
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            foreach (var type in GetTypesFromNamespace(compilation.Assembly.GlobalNamespace))
            {
                var key = type.ToDisplayString();
                if (seen.Add(key))
                    yield return type;
            }
            break; // GlobalNamespace is the same for all trees
        }

        // If no syntax trees (runtime/package mode), get from assembly
        if (!compilation.SyntaxTrees.Any())
        {
            foreach (var type in GetTypesFromNamespace(compilation.Assembly.GlobalNamespace))
            {
                var key = type.ToDisplayString();
                if (seen.Add(key))
                    yield return type;
            }
        }

        // Types from metadata references
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is IAssemblySymbol assembly)
            {
                foreach (var type in GetTypesFromNamespace(assembly.GlobalNamespace))
                {
                    var key = type.ToDisplayString();
                    if (seen.Add(key))
                        yield return type;
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public ||
                type.DeclaredAccessibility == Accessibility.Protected)
            {
                yield return type;
                foreach (var nested in GetNestedTypes(type))
                    yield return nested;
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetTypesFromNamespace(childNs))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            if (nested.DeclaredAccessibility == Accessibility.Public ||
                nested.DeclaredAccessibility == Accessibility.Protected)
            {
                yield return nested;
                foreach (var deepNested in GetNestedTypes(nested))
                    yield return deepNested;
            }
        }
    }
}
