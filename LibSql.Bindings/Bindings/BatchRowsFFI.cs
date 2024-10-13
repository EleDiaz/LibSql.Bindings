using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class BatchRowsHandle : LibSqlSafeHandle
{
    public BatchRowsHandle(nint ptr)
        : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        BatchRows.libsql_free_batchrows(handle);
        return true;
    }
}

public partial class BatchRows
{
    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_next_stmt_row_batchrows")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_next_stmt_row_batchrows(
        BatchRowsHandle batchRowsHandle,
        out IntPtr out_rows
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_batchrows")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_batchrows(IntPtr batchrows);
}
