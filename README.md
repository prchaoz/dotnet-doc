# dotnet-describe

A `go doc` analog for .NET. Displays type documentation, member signatures, and XML doc comments from the command line.

Built for AI agents that need to implement interfaces or override classes without an IDE.

## Installation

```bash
dotnet pack src/DotnetDescribe/DotnetDescribe.csproj
dotnet tool install -g --add-source src/DotnetDescribe/bin/Release/net10.0/ dotnet-describe
```

## Usage

Exactly one source mode is required: `--runtime`, `--package`, or `--project`.

### Inspect runtime/framework types

```bash
dotnet-describe --runtime netcore/10.0 IDisposable
dotnet-describe --runtime aspnet/10.0 IApplicationBuilder
dotnet-describe --runtime netcore/10.0 System.IO.Stream
dotnet-describe --runtime netcore/10.0 Stream.Read
```

Short aliases: `netcore`, `aspnet`, `desktop`.

### Inspect NuGet packages

```bash
dotnet-describe --package Microsoft.Extensions.Logging.Abstractions/9.0.0 ILogger
dotnet-describe --package Newtonsoft.Json/13.0.3 JsonConvert
```

Packages are resolved from the NuGet global cache (`~/.nuget/packages/`).

### Inspect project source code

```bash
dotnet-describe --project ./src/MyApp MyApp.Services.UserService
dotnet-describe --project ./src/MyApp IUserService
```

Uses Roslyn to analyze source code directly. Works even if the project doesn't compile. Shows source file location in output.

### Options

| Option | Description |
|---|---|
| `--all`, `-a` | Show all members including inherited |
| `--private`, `-u` | Show non-public members |
| `--framework`, `-f` | Target framework override (e.g., `net8.0`) |
| `--no-refs` | Suppress referenced types footer |

## Output

### Interface

```
$ dotnet-describe --runtime netcore/10.0 IDisposable

namespace System

public interface IDisposable
    // Performs application-defined tasks associated with freeing,
    // releasing, or resetting unmanaged resources.
    void Dispose()
```

### Abstract class with referenced types

```
$ dotnet-describe --runtime netcore/10.0 System.IO.Stream

namespace System.IO

public abstract class Stream : MarshalByRefObject, IAsyncDisposable, IDisposable
    public abstract bool CanRead { get; }
    public abstract bool CanSeek { get; }
    public abstract bool CanWrite { get; }
    public abstract long Length { get; }
    public abstract long Position { get; set; }

    // When overridden in a derived class, reads a sequence of bytes
    // from the current stream.
    public abstract int Read(byte[] buffer, int offset, int count)
    public virtual int Read(Span<byte> buffer)
    public abstract void Write(byte[] buffer, int offset, int count)
    public abstract long Seek(long offset, SeekOrigin origin)
    public abstract void Flush()
    ...

Referenced types:
    runtime: Microsoft.NETCore.App/10.0
        System.IAsyncDisposable                           interface
        System.IDisposable                                interface
        System.IO.SeekOrigin                              enum
        System.MarshalByRefObject                         class
        System.Span<T>                                    struct
        ...
```

### Member detail

```
$ dotnet-describe --runtime netcore/10.0 Stream.Read

namespace System.IO, class Stream

public abstract int Read(byte[] buffer, int offset, int count)
    When overridden in a derived class, reads a sequence of bytes from
    the current stream.

    Parameters:
        buffer  - An array of bytes.
        offset  - The zero-based byte offset in buffer.
        count   - The maximum number of bytes to be read.

    Returns:
        The total number of bytes read into the buffer.
```

### NuGet package

```
$ dotnet-describe --package Microsoft.Extensions.Logging.Abstractions/9.0.0 ILogger

namespace Microsoft.Extensions.Logging

public interface ILogger
    // Writes a log entry.
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    // Checks if the given logLevel is enabled.
    bool IsEnabled(LogLevel logLevel)
    // Begins a logical operation scope.
    IDisposable? BeginScope<TState>(TState state)

Referenced types:
    package: Microsoft.Extensions.Logging.Abstractions/9.0.0
        Microsoft.Extensions.Logging.EventId              struct
        Microsoft.Extensions.Logging.LogLevel             enum
    runtime: Microsoft.NETCore.App/8.0
        System.Exception                                  class
        System.Func<T1, T2, TResult>                      delegate
        System.IDisposable                                interface
```

## Referenced types

The footer groups non-primitive types used in signatures by their source, with labels that tell you how to inspect them further:

```
Referenced types:
    package: Microsoft.Extensions.Logging.Abstractions/9.0.0
        Microsoft.Extensions.Logging.LogLevel             enum
    runtime: Microsoft.NETCore.App/10.0
        System.Exception                                  class
```

To drill into a referenced type, use the label as the source flag:

```bash
dotnet-describe --package Microsoft.Extensions.Logging.Abstractions/9.0.0 Microsoft.Extensions.Logging.LogLevel
```

## Architecture

All three modes use Roslyn. The difference is how the `Compilation` is constructed:

| Mode | Compilation source |
|---|---|
| `--runtime` | `CSharpCompilation` + `MetadataReference` from SDK `packs/` directory |
| `--package` | `CSharpCompilation` + `MetadataReference` from NuGet cache + runtime refs |
| `--project` | `MSBuildWorkspace` + source code analysis |

## License

MIT
