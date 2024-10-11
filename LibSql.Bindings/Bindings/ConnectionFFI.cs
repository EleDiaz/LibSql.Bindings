using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class ConnectionHandle : SafeHandle
{
    public ConnectionHandle(IntPtr connection)
        : base(IntPtr.Zero, true)
    {
        SetHandle(connection);
    }

    public ConnectionHandle(IntPtr connection, bool owned)
        : base(IntPtr.Zero, owned)
    {
        SetHandle(connection);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Connection.libsql_disconnect(handle);
        return true;
    }
}

public partial class Connection
{
    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_load_extension",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_load_extension(
        SafeHandle conn,
        string path,
        string? entry_point,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_transaction_with_behavior")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_transaction_with_behavior(SafeHandle conn, out IntPtr out_transaction, int transaction_behavior, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_reset")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_reset(SafeHandle conn, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_disconnect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_disconnect(IntPtr conn);

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_prepare",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_prepare(
        SafeHandle conn,
        string sql,
        out IntPtr out_stmt,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_query",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_query(
        SafeHandle conn,
        string sql,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_query_positional",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_query_positional(
        SafeHandle conn,
        string sql,
        IntPtr in_positional_values,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_query",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_query_named(
        SafeHandle conn,
        string sql,
        IntPtr in_named_values,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_execute_none",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_execute_none(
        SafeHandle conn,
        string sql,
        out ulong out_rows_change, // CULong we discard the 32bits platforms, although this is far from being critical
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_execute_positional",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_execute_positional(
        SafeHandle conn,
        string sql,
        IntPtr in_positional_values,
        out ulong out_rows_change,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_execute_named",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_execute_named(
        SafeHandle conn,
        string sql,
        IntPtr in_named_values,
        out ulong out_rows_change,
        out IntPtr out_err_msg
    );

    // [LibraryImport(Utils.__DllName, EntryPoint = "libsql_execute_batch", StringMarshalling = StringMarshalling.Utf8)]
    // internal static partial int libsql_execute_batch(SafeHandle conn, string sql, batch_rows_t* out_batch_rows, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_changes")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial ulong libsql_changes(ConnectionHandle conn);

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_last_insert_rowid",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial long libsql_last_insert_rowid(ConnectionHandle conn);
}
