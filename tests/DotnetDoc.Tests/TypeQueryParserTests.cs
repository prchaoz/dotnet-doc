using DotnetDoc.TypeResolution;

namespace DotnetDoc.Tests;

public class TypeQueryParserTests
{
    [Fact]
    public void Parse_SimpleName_ReturnsTypeOnly()
    {
        var results = TypeQueryParser.Parse("IDisposable");
        Assert.Single(results);
        Assert.Null(results[0].Namespace);
        Assert.Equal("IDisposable", results[0].TypeName);
        Assert.Null(results[0].MemberName);
    }

    [Fact]
    public void Parse_TwoParts_ReturnsTypeMemberFirst()
    {
        var results = TypeQueryParser.Parse("Stream.Read");
        Assert.True(results.Count >= 2);
        // First interpretation: Type.Member
        Assert.Null(results[0].Namespace);
        Assert.Equal("Stream", results[0].TypeName);
        Assert.Equal("Read", results[0].MemberName);
    }

    [Fact]
    public void Parse_ThreeParts_NamespaceTypeMember()
    {
        var results = TypeQueryParser.Parse("System.IO.Stream");
        Assert.True(results.Count >= 1);
        // First interpretation: namespace.type.member
        var first = results[0];
        Assert.Equal("System", first.Namespace);
        Assert.Equal("IO", first.TypeName);
        Assert.Equal("Stream", first.MemberName);
        // Second interpretation: namespace.type
        var second = results[1];
        Assert.Equal("System.IO", second.Namespace);
        Assert.Equal("Stream", second.TypeName);
        Assert.Null(second.MemberName);
    }

    [Fact]
    public void Parse_FourParts_NamespaceTypeMember()
    {
        var results = TypeQueryParser.Parse("System.IO.Stream.Read");
        var first = results[0];
        Assert.Equal("System.IO", first.Namespace);
        Assert.Equal("Stream", first.TypeName);
        Assert.Equal("Read", first.MemberName);
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => TypeQueryParser.Parse(""));
        Assert.Throws<ArgumentException>(() => TypeQueryParser.Parse("   "));
    }
}
