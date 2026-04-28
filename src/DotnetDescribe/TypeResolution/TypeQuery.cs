namespace DotnetDescribe.TypeResolution;

public record TypeQuery(
    string? Namespace,
    string TypeName,
    string? MemberName
);
