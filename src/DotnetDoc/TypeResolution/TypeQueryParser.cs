namespace DotnetDoc.TypeResolution;

public static class TypeQueryParser
{
    /// <summary>
    /// Parses a user query string into possible TypeQuery interpretations.
    /// Returns multiple interpretations ordered by likelihood (most specific first).
    ///
    /// Examples:
    ///   "IDisposable" → [(null, "IDisposable", null)]
    ///   "System.IO.Stream" → [("System.IO", "Stream", null), ("System", "IO", "Stream")]
    ///   "Stream.Read" → [(null, "Stream", "Read")]
    ///   "System.IO.Stream.Read" → [("System.IO", "Stream", "Read"), ("System.IO.Stream", "Read", null)]
    /// </summary>
    public static List<TypeQuery> Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Type or member name cannot be empty.");

        var parts = input.Split('.');
        var results = new List<TypeQuery>();

        if (parts.Length == 1)
        {
            // Simple name: just a type
            results.Add(new TypeQuery(null, parts[0], null));
            return results;
        }

        if (parts.Length == 2)
        {
            // Could be Type.Member or Namespace.Type
            // Prefer Type.Member interpretation first (more common query)
            results.Add(new TypeQuery(null, parts[0], parts[1]));
            results.Add(new TypeQuery(null, input, null)); // Full string as type name (nested type?)
            results.Add(new TypeQuery(parts[0], parts[1], null));
            return results;
        }

        // 3+ parts: try interpretations right-to-left
        // Interpretation 1: last part is member, second-to-last is type, rest is namespace
        var ns1 = string.Join(".", parts[..^2]);
        results.Add(new TypeQuery(ns1, parts[^2], parts[^1]));

        // Interpretation 2: last part is type, rest is namespace (no member)
        var ns2 = string.Join(".", parts[..^1]);
        results.Add(new TypeQuery(ns2, parts[^1], null));

        // Interpretation 3: last two parts form the type name (e.g., nested type), rest is namespace
        if (parts.Length >= 3)
        {
            var ns3 = string.Join(".", parts[..^2]);
            results.Add(new TypeQuery(ns3.Length > 0 ? ns3 : null, $"{parts[^2]}.{parts[^1]}", null));
        }

        // Interpretation 4: no namespace, second part is type, last is member
        // (handles unqualified like "MyClass.MyMethod" with accidental namespace-like prefix)
        results.Add(new TypeQuery(null, parts[^2], parts[^1]));

        return results;
    }
}
