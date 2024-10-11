using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class RowsHandle : LibSqlSafeHandle
{
    public RowsHandle(nint ptr)
        : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        Rows.libsql_free_rows(handle);
        return true;
    }
}

public partial class Rows : IDisposable
{
    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_next_row")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_next_row(
        RowsHandle rows,
        out IntPtr out_row,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_column_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_column_count(RowsHandle rows);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_column_name")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_column_name(
        RowsHandle rows,
        int col,
        out IntPtr out_name,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_column_type")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_column_type(
        RowsHandle rows,
        IntPtr row_,
        int col,
        out int out_type,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_rows")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_rows(IntPtr rows);
}
