---
name: dotnet-method-documentation
description: Inspect .NET method signatures, parameters, return types, and exceptions using dotnet-describe CLI. Use when calling, overriding, or wrapping a specific method and you need its exact contract.
when_to_use: Automatically use when you need to understand a specific method's parameters, return type, exceptions, or behavior before calling or overriding it.
---

Before calling, overriding, or wrapping a method, run `dotnet-describe` with the `Type.Method` syntax to see its full documentation.

## Commands

Inspect a specific method (shows all overloads, parameters, return type, exceptions):
```bash
dotnet-describe --runtime netcore/10.0 Stream.Read
dotnet-describe --package Microsoft.Extensions.Logging.Abstractions/9.0.0 ILogger.Log
dotnet-describe --project ./src/MyApp UserService.CreateAsync
```

Fully qualified:
```bash
dotnet-describe --runtime netcore/10.0 System.IO.Stream.Read
```

## Workflow

1. Identify the method and its containing type
2. Run `dotnet-describe` with `Type.Method` to see all overloads
3. Review:
   - **Parameters** — names, types, and descriptions
   - **Returns** — what the method returns and when
   - **Exceptions** — what can be thrown and under what conditions
4. If a parameter or return type is unfamiliar, inspect it using `dotnet-describe` with the type name
