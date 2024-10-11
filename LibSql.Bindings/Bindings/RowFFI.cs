using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class RowHandle : LibSqlSafeHandle
{
    public RowHandle(nint ptr)
        : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        Row.libsql_free_row(handle);
        return true;
    }
}

public partial class Row
{
    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_row")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_row(IntPtr row);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_get_string")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_get_string(
        RowHandle row,
        int col,
        out IntPtr out_value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_get_int")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_get_int(
        RowHandle row,
        int col,
        out long out_value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_get_float")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_get_float(
        RowHandle row,
        int col,
        out double out_value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_get_blob")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_get_blob(
        RowHandle row,
        int col,
        out BlobRaw out_blob,
        out IntPtr out_err_msg
    );
}
