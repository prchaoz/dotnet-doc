---
name: dotnet-type-documentation
description: Inspect .NET type signatures and documentation before implementing interfaces or overriding classes. Use dotnet-describe CLI to look up type members, XML doc comments, and referenced types.
when_to_use: Automatically use when implementing a .NET interface, overriding an abstract or virtual class, or when you need to understand a type's API surface from a NuGet package, runtime, or project source.
---

Before implementing an interface or overriding a class, run `dotnet-describe` to inspect the type's full API surface.

## How to determine the source mode

- **Runtime type** (System.*, Microsoft.Extensions.* from framework): `--runtime netcore/<version>` or `--runtime aspnet/<version>`
- **NuGet package type**: `--package <PackageId>/<Version>` — check the project's .csproj or Directory.Packages.props for the version
- **Project type**: `--project <path-to-csproj>`

## Commands

Inspect a type (shows all members, doc comments, referenced types):
```bash
dotnet-describe --runtime netcore/10.0 System.IO.Stream
dotnet-describe --package Microsoft.Extensions.Logging.Abstractions/9.0.0 ILogger
dotnet-describe --project ./src/MyApp IUserService
```

Include inherited members:
```bash
dotnet-describe --runtime netcore/10.0 --all System.IO.Stream
```

## Workflow

1. Identify the type you need to implement/override
2. Determine its source (runtime, package, or project) and version
3. Run `dotnet-describe` to see all members, their signatures, and doc comments
4. Check the "Referenced types" footer — if a parameter or return type is unfamiliar, inspect it too using the source label shown
5. Implement all required members with correct signatures
