# LibSql.Bindings

Bindings in progress for the Rust library [libsql](https://github.com/tursodatabase/libsql), designed to integrate with .NET.

This library takes a different approach than [https://github.com/tvandinther/libsql-client-dotnet](https://github.com/tvandinther/libsql-client-dotnet). Although both are based on C bindings of libSQL, I have made some modifications to those bindings, removing some wrapper types (no idea what practical use can have for C#) and filling all the missing API.

This project doesn't use the **csbindgen** tooling, though I wouldn't mind adding it back if it could generate files like my *FFI.cs.

The goal is to provide a simple foundation to work with, avoiding minor marshalling issues around the FFI. That's why I'm using **LibraryImport** from **.NET 7** and the **SafeHandle** API where possible.

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

- Transaction handling: The actual transaction API feels prone to error (inherited from rust lib)
- Batch rows: Needs Testing
- Testing: More comprehensive test coverage is needed. And test for leaks.

## Future Considerations

- ADO.NET + Entity Framework (EF) Integration: Potentially add support for ADO.NET and Entity Framework, though this is not yet a focus.

## License

This repository falls under the MIT license.

