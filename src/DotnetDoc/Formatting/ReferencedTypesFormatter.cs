using System.Text;
using DotnetDoc.AssemblyResolution;
using DotnetDoc.CompilationFactory;
using Microsoft.CodeAnalysis;

namespace DotnetDoc.Formatting;

public static class ReferencedTypesFormatter
{
    private static readonly HashSet<SpecialType> SkipSpecialTypes =
    [
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_Decimal,
        SpecialType.System_String,
        SpecialType.System_Char,
        SpecialType.System_Object,
        SpecialType.System_Void,
        SpecialType.System_IntPtr,
        SpecialType.System_UIntPtr,
    ];

    public static string Format(
        INamedTypeSymbol inspectedType,
        List<ISymbol> members,
        CompilationResult compilationResult)
    {
        var referencedTypes = CollectReferencedTypes(inspectedType, members);
        if (referencedTypes.Count == 0)
            return "";

        // Group by source
        var grouped = new Dictionary<string, List<(INamedTypeSymbol Type, string Kind)>>();

        foreach (var type in referencedTypes)
        {
            var sourceLabel = ResolveSourceLabel(type, compilationResult);
            var labelKey = $"{sourceLabel.Kind.ToString().ToLowerInvariant()}: {sourceLabel.Identifier}";
            var kind = GetTypeKindString(type);

            if (!grouped.ContainsKey(labelKey))
                grouped[labelKey] = [];

            grouped[labelKey].Add((type, kind));
        }

        var sb = new StringBuilder();
        sb.AppendLine("Referenced types:");

        // Order: project first, then package, then runtime
        var orderedGroups = grouped
            .OrderBy(g => g.Key.StartsWith("project") ? 0 : g.Key.StartsWith("package") ? 1 : 2)
            .ThenBy(g => g.Key);

        foreach (var group in orderedGroups)
        {
            sb.AppendLine($"    {group.Key}");
            foreach (var (type, kind) in group.Value.OrderBy(t => t.Type.ToDisplayString()))
            {
                var displayName = FormatTypeDisplayName(type);
                var padding = Math.Max(1, 50 - displayName.Length);
                sb.AppendLine($"        {displayName}{new string(' ', padding)}{kind}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static HashSet<INamedTypeSymbol> CollectReferencedTypes(
        INamedTypeSymbol inspectedType,
        List<ISymbol> members)
    {
        var referenced = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var inspectedAssembly = inspectedType.ContainingAssembly;

        // Base types
        if (inspectedType.BaseType != null)
            CollectFromType(inspectedType.BaseType, referenced);

        // Interfaces
        foreach (var iface in inspectedType.Interfaces)
            CollectFromType(iface, referenced);

        // Members
        foreach (var member in members)
        {
            switch (member)
            {
                case IMethodSymbol method:
                    CollectFromType(method.ReturnType, referenced);
                    foreach (var param in method.Parameters)
                        CollectFromType(param.Type, referenced);
                    break;

                case IPropertySymbol property:
                    CollectFromType(property.Type, referenced);
                    break;

                case IFieldSymbol field:
                    CollectFromType(field.Type, referenced);
                    break;

                case IEventSymbol evt:
                    CollectFromType(evt.Type, referenced);
                    break;
            }
        }

        // Remove the inspected type itself and types from same namespace that are trivial
        referenced.Remove(inspectedType);

        return referenced;
    }

    private static void CollectFromType(ITypeSymbol? type, HashSet<INamedTypeSymbol> collected)
    {
        if (type == null) return;

        if (type is IArrayTypeSymbol arrayType)
        {
            CollectFromType(arrayType.ElementType, collected);
            return;
        }

        if (type is INamedTypeSymbol namedType)
        {
            // Skip primitive/special types
            if (SkipSpecialTypes.Contains(namedType.SpecialType))
                return;

            // Skip type parameters
            if (namedType.TypeKind == TypeKind.TypeParameter)
                return;

            // Skip System.Nullable<T>, but collect T
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                foreach (var arg in namedType.TypeArguments)
                    CollectFromType(arg, collected);
                return;
            }

            collected.Add(namedType.OriginalDefinition);

            // Also collect generic type arguments
            foreach (var arg in namedType.TypeArguments)
                CollectFromType(arg, collected);
        }
    }

    private static SourceLabel ResolveSourceLabel(
        INamedTypeSymbol type,
        CompilationResult compilationResult)
    {
        var containingAssembly = type.ContainingAssembly;
        if (containingAssembly == null)
            return new SourceLabel(SourceKind.Runtime, "unknown");

        var asmName = containingAssembly.Name;
        if (compilationResult.AssemblySourceMap.TryGetValue(asmName, out var label))
            return label;

        return new SourceLabel(SourceKind.Runtime, "unknown");
    }

    private static string GetTypeKindString(INamedTypeSymbol type)
    {
        if (type.IsRecord) return "record";
        return type.TypeKind switch
        {
            TypeKind.Class => type.IsStatic ? "static class" : "class",
            TypeKind.Interface => "interface",
            TypeKind.Struct => "struct",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => type.TypeKind.ToString().ToLowerInvariant()
        };
    }

    private static string FormatTypeDisplayName(INamedTypeSymbol type)
    {
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        return type.OriginalDefinition.ToDisplayString(format);
    }
}
