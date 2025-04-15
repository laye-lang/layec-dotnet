# dnlayec
An implementation of the Laye module compiler, written in C#.

## Building from source

Ensure you have the necessary prerequisites installed:
- .NET CLI tooling
- .NET 9.0 SDK

Or use your IDE of choice with .NET 9.0 capabilities.

To build in Debug mode:
```sh
$ dotnet build src/LayeC
```
which will output to `src/LayeC/bin/Debug/<net-version>/` by default.
Or, to create an AOT compiled native executable:
```sh
$ dotnet publish src/LayeC -r <target> -c Release -p:PublishAot=true
```
where `<target>` is the platform runtime identifier for your system, such as `linux-x64` or `win-x64`, which will output to `src/LayeC/bin/Release/<net-version>/<target>/publish/` by default. You can find all known runtime identifiers [here](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog#known-rids).

When published AOT, a native executable for the target is produced; there is then no dependency on the .NET framework or CLR.

## Usage

For general usage, it's recommended to follow the publishing steps in [Building from source](#building-from-source) and manually installing the binary somewhere that makes sense for you.

> *NOTE: Currently, this version of the compiler is not capable of generating binary files and as such does not require searching for libraries. When Laye module binaries are supported, you'll want to also build and install the required core libraries.*

Otherwise to use the compiler while developing it, you have a few options:
- add the debug output directory to your PATH,
- always use the full path to the compiler, or
- use `dotnet run src/LayeC -- [compiler arguments]` instead of `dotnet build`.

No matter how you run it, see the `--help` output for more information.

> *TODO: Provide some actually useful documentation.*
