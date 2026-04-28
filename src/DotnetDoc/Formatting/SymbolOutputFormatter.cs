using System.Text;
using System.Xml.Linq;
using DotnetDoc.AssemblyResolution;
using DotnetDoc.CompilationFactory;
using Microsoft.CodeAnalysis;

namespace DotnetDoc.Formatting;

public class SymbolOutputFormatter
{
    private static readonly SymbolDisplayFormat TypeDeclarationFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
        memberOptions: SymbolDisplayMemberOptions.None
    );

    private static readonly SymbolDisplayFormat MemberSignatureFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
                       | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeType
                     | SymbolDisplayMemberOptions.IncludeParameters
                     | SymbolDisplayMemberOptions.IncludeAccessibility
                     | SymbolDisplayMemberOptions.IncludeModifiers
                     | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
                        | SymbolDisplayParameterOptions.IncludeName
                        | SymbolDisplayParameterOptions.IncludeDefaultValue
                        | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor
    );

    private readonly CompilationResult _compilationResult;
    private readonly bool _showAll;
    private readonly bool _showPrivate;
    private readonly bool _showRefs;
    private readonly bool _isProjectMode;

    public SymbolOutputFormatter(
        CompilationResult compilationResult,
        bool showAll,
        bool showPrivate,
        bool showRefs,
        bool isProjectMode)
    {
        _compilationResult = compilationResult;
        _showAll = showAll;
        _showPrivate = showPrivate;
        _showRefs = showRefs;
        _isProjectMode = isProjectMode;
    }

    public string FormatType(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();

        // Source file location for project mode
        if (_isProjectMode)
        {
            var location = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation();
            if (location?.SourceTree != null)
            {
                var lineSpan = location.GetLineSpan();
                sb.AppendLine($"// {lineSpan.Path}:{lineSpan.StartLinePosition.Line + 1}");
            }
        }

        // Namespace
        var ns = type.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
            sb.AppendLine($"namespace {ns}");

        sb.AppendLine();

        // Type declaration with base types
        sb.AppendLine(FormatTypeDeclaration(type));

        // Members
        var members = GetDisplayMembers(type);
        foreach (var group in GroupMembers(members))
        {
            foreach (var member in group)
            {
                var doc = GetSummary(member);
                if (!string.IsNullOrWhiteSpace(doc))
                    sb.AppendLine($"    // {doc}");
                sb.AppendLine($"    {FormatMember(member)}");
            }
            sb.AppendLine();
        }

        // Referenced types footer
        if (_showRefs)
        {
            var refsOutput = ReferencedTypesFormatter.Format(type, members, _compilationResult);
            if (!string.IsNullOrEmpty(refsOutput))
            {
                sb.AppendLine(refsOutput);
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public string FormatMember(INamedTypeSymbol containingType, ISymbol member)
    {
        var sb = new StringBuilder();

        // Source file location for project mode
        if (_isProjectMode)
        {
            var location = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation();
            if (location?.SourceTree != null)
            {
                var lineSpan = location.GetLineSpan();
                sb.AppendLine($"// {lineSpan.Path}:{lineSpan.StartLinePosition.Line + 1}");
            }
        }

        var ns = containingType.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
            sb.Append($"namespace {ns}, ");
        sb.AppendLine($"{containingType.TypeKind.ToString().ToLowerInvariant()} {containingType.Name}");

        sb.AppendLine();
        sb.AppendLine(FormatMember(member));

        // Full documentation
        var docXml = member.GetDocumentationCommentXml();
        if (!string.IsNullOrWhiteSpace(docXml))
        {
            var doc = ParseDocComment(docXml);
            if (doc.Summary != null)
            {
                sb.AppendLine($"    {doc.Summary}");
                sb.AppendLine();
            }

            if (doc.Parameters.Count > 0)
            {
                sb.AppendLine("    Parameters:");
                foreach (var (name, desc) in doc.Parameters)
                    sb.AppendLine($"        {name} - {desc}");
                sb.AppendLine();
            }

            if (doc.Returns != null)
            {
                sb.AppendLine($"    Returns:");
                sb.AppendLine($"        {doc.Returns}");
                sb.AppendLine();
            }

            if (doc.Exceptions.Count > 0)
            {
                sb.AppendLine("    Exceptions:");
                foreach (var (type, desc) in doc.Exceptions)
                    sb.AppendLine($"        {type} - {desc}");
                sb.AppendLine();
            }

            if (doc.Remarks != null)
            {
                sb.AppendLine($"    Remarks:");
                sb.AppendLine($"        {doc.Remarks}");
            }
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private string FormatTypeDeclaration(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();

        // Accessibility
        sb.Append(FormatAccessibility(type.DeclaredAccessibility));

        // Modifiers
        if (type.IsStatic) sb.Append("static ");
        if (type.IsAbstract && type.TypeKind == TypeKind.Class) sb.Append("abstract ");
        if (type.IsSealed && type.TypeKind == TypeKind.Class && !type.IsStatic) sb.Append("sealed ");

        // Type keyword + name
        if (type.IsRecord) sb.Append("record ");
        sb.Append(type.ToDisplayString(TypeDeclarationFormat));

        // Base type and interfaces
        var bases = new List<string>();
        if (type.BaseType != null &&
            type.BaseType.SpecialType != SpecialType.System_Object &&
            type.BaseType.SpecialType != SpecialType.System_ValueType &&
            type.BaseType.SpecialType != SpecialType.System_Enum)
        {
            bases.Add(type.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        foreach (var iface in type.Interfaces)
            bases.Add(iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

        if (bases.Count > 0)
            sb.Append($" : {string.Join(", ", bases)}");

        return sb.ToString();
    }

    private List<ISymbol> GetDisplayMembers(INamedTypeSymbol type)
    {
        var members = new List<ISymbol>();
        var seen = new HashSet<string>();

        // Own members first
        AddMembers(type, members, seen, isInherited: false);

        // Inherited members if --all
        if (_showAll)
        {
            var current = type.BaseType;
            while (current != null &&
                   current.SpecialType != SpecialType.System_Object &&
                   current.SpecialType != SpecialType.System_ValueType)
            {
                AddMembers(current, members, seen, isInherited: true);
                current = current.BaseType;
            }

            // Interface members (for non-interface types)
            if (type.TypeKind != TypeKind.Interface)
            {
                foreach (var iface in type.AllInterfaces)
                    AddMembers(iface, members, seen, isInherited: true);
            }
        }

        return members;
    }

    private void AddMembers(INamedTypeSymbol type, List<ISymbol> members, HashSet<string> seen, bool isInherited)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared) continue;
            if (member is IMethodSymbol { MethodKind: not MethodKind.Ordinary }) continue;

            if (!_showPrivate)
            {
                if (member.DeclaredAccessibility != Accessibility.Public &&
                    member.DeclaredAccessibility != Accessibility.Protected &&
                    member.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
                    continue;
            }

            var key = member.ToDisplayString(MemberSignatureFormat);
            if (seen.Add(key))
                members.Add(member);
        }
    }

    private static string FormatMember(ISymbol member)
    {
        return member.ToDisplayString(MemberSignatureFormat);
    }

    private static List<List<ISymbol>> GroupMembers(List<ISymbol> members)
    {
        var groups = new List<List<ISymbol>>();

        var fields = members.Where(m => m is IFieldSymbol).ToList();
        var properties = members.Where(m => m is IPropertySymbol).ToList();
        var methods = members.Where(m => m is IMethodSymbol).ToList();
        var events = members.Where(m => m is IEventSymbol).ToList();

        if (fields.Count > 0) groups.Add(fields);
        if (properties.Count > 0) groups.Add(properties);
        if (methods.Count > 0) groups.Add(methods);
        if (events.Count > 0) groups.Add(events);

        return groups;
    }

    private static string? GetSummary(ISymbol symbol)
    {
        var docXml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(docXml))
            return null;

        var doc = ParseDocComment(docXml);
        return doc.Summary;
    }

    private static string FormatAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Protected => "protected ",
            Accessibility.Internal => "internal ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            Accessibility.ProtectedAndInternal => "private protected ",
            Accessibility.Private => "private ",
            _ => ""
        };
    }

    private record DocComment(string? Summary, List<(string Name, string Desc)> Parameters, string? Returns, string? Remarks, List<(string Type, string Desc)> Exceptions);

    private static DocComment ParseDocComment(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return new DocComment(null, [], null, null, []);

            var summary = RenderElement(root.Element("summary"));
            var returns = RenderElement(root.Element("returns"));
            var remarks = RenderElement(root.Element("remarks"));

            var parameters = root.Elements("param")
                .Select(p => (
                    Name: p.Attribute("name")?.Value ?? "",
                    Desc: RenderElement(p) ?? ""))
                .Where(p => !string.IsNullOrEmpty(p.Name))
                .ToList();

            var exceptions = root.Elements("exception")
                .Select(e => (
                    Type: RenderCref(e.Attribute("cref")?.Value) ?? "",
                    Desc: RenderElement(e) ?? ""))
                .Where(e => !string.IsNullOrEmpty(e.Type))
                .ToList();

            return new DocComment(summary, parameters, returns, remarks, exceptions);
        }
        catch
        {
            return new DocComment(null, [], null, null, []);
        }
    }

    private static string? RenderElement(XElement? element)
    {
        if (element == null) return null;

        var sb = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;
                case XElement el:
                    sb.Append(RenderInlineElement(el));
                    break;
            }
        }

        // Collapse whitespace
        var result = string.Join(" ", sb.ToString().Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string RenderInlineElement(XElement el)
    {
        return el.Name.LocalName switch
        {
            "see" => RenderCref(el.Attribute("cref")?.Value)
                  ?? el.Attribute("langword")?.Value
                  ?? RenderElement(el)
                  ?? "",
            "seealso" => RenderCref(el.Attribute("cref")?.Value) ?? "",
            "paramref" => el.Attribute("name")?.Value ?? "",
            "typeparamref" => el.Attribute("name")?.Value ?? "",
            "c" => el.Value,
            "code" => el.Value,
            "para" => RenderElement(el) ?? "",
            _ => RenderElement(el) ?? "",
        };
    }

    private static string? RenderCref(string? cref)
    {
        if (cref == null) return null;
        return cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
    }
}
