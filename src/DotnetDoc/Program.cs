using System.CommandLine;
using DotnetDoc;

var typeOrMemberArg = new Argument<string>("type-or-member")
{
    Description = "Type name or Type.Member to show documentation for"
};

var runtimeOption = new Option<string?>("--runtime", "-r")
{
    Description = "Runtime to inspect (e.g., netcore/8.0, aspnet/8.0)",
};

var packageOption = new Option<string?>("--package", "-p")
{
    Description = "NuGet package to inspect (e.g., Newtonsoft.Json/13.0.3)",
};

var projectOption = new Option<string?>("--project")
{
    Description = "Project path to inspect (.csproj or directory)",
};

var allOption = new Option<bool>("--all", "-a")
{
    Description = "Show all members including inherited",
};

var privateOption = new Option<bool>("--private", "-u")
{
    Description = "Show non-public members",
};

var frameworkOption = new Option<string?>("--framework", "-f")
{
    Description = "Target framework override (e.g., net8.0)",
};

var noRefsOption = new Option<bool>("--no-refs")
{
    Description = "Suppress referenced types footer",
};

var rootCommand = new RootCommand("Display documentation for .NET types and members")
{
    typeOrMemberArg,
    runtimeOption,
    packageOption,
    projectOption,
    allOption,
    privateOption,
    frameworkOption,
    noRefsOption,
};

rootCommand.SetAction(async parseResult  =>
{
    var options = new DocOptions(
        Runtime: parseResult.GetValue(runtimeOption),
        Package: parseResult.GetValue(packageOption),
        Project: parseResult.GetValue(projectOption),
        TypeOrMember: parseResult.GetRequiredValue(typeOrMemberArg),
        ShowAll: parseResult.GetValue(allOption),
        ShowPrivate: parseResult.GetValue(privateOption),
        Framework: parseResult.GetValue(frameworkOption),
        NoRefs: parseResult.GetValue(noRefsOption)
    );

    await DocCommand.ExecuteAsync(options);
});

return await rootCommand.Parse(args).InvokeAsync();