# LibSql.Bindings

Bindings in progress for the Rust library [libsql](https://github.com/tursodatabase/libsql), designed to integrate with .NET. 

This library takes a different approach than [https://github.com/tvandinther/libsql-client-dotnet](https://github.com/tvandinther/libsql-client-dotnet), although both are based on c-bindings of libSql, I had added some modifications to those bindings, and this project don't use **csbindgen** tooling, although I wouldn't mind to be added back, if it was able to generate files like my *FFI.cs

So, the idea is to provide a simple base to work with, without having to deal with minor marshalling issues around the FFI. And that is why the usage of **LibraryImport** from **.NET 7**, SafeHandler API where is possible.

## Build Instructions

To build the project, ensure you have the following tools installed:

- [Rust](https://www.rust-lang.org/tools/install)
- [Cross](https://github.com/cross-rs/cross)
- [.NET SDK](https://dotnet.microsoft.com/en-us/)

Currently, this library supports Linux 64-bit (`linux-x64`) and AArch64 (`linux-arm`). Adding support for other platforms should be straightforward, so far, as they are supported by the original `libsql` implementation.

### Build Command

To build for a specific runtime (e.g., `linux-x64`), run:

```bash
dotnet build -c Release --runtime linux-x64
```

## Features (Not battle tested, there may be dragons)

- **Connection**: Supports connections to local, remote, and replicated databases.
- **Querying**: Execute queries using both positional and named parameters.
- **Statements**: Supports the execution of SQL statements.

## Current Limitations

The following features are either incomplete or require further development as of this writing:

- Batch rows: Needs implementation.
- Transaction handling: The actual transaction API feels prone to error (inherited from rust lib)
- Safe handlers: Certain utility classes (in Utils.cs) could benefit from safer memory handling.
- Testing: More comprehensive test coverage is needed.

## Future Considerations

- ADO.NET + Entity Framework (EF) Integration: Potentially add support for ADO.NET and Entity Framework, though this is not yet a focus.

## License

This repository falls under the MIT license.

