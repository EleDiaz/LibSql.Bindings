using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class StatementsHandle : LibSqlSafeHandle
{
    public StatementsHandle(nint ptr)
        : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        Statements.libsql_free_stmt(handle);
        return true;
    }
}

public partial class Statements
{
    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_query_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_query_stmt(
        StatementsHandle statements,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_query_stmt_positional")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_query_stmt_positional(
        StatementsHandle statements,
        IntPtr positional,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_query_stmt_named")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_query_stmt_named(
        StatementsHandle statements,
        IntPtr named,
        out IntPtr out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_execute_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_execute_stmt(
        StatementsHandle statements,
        out ulong out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_execute_stmt_positional")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_execute_stmt_positional(
        StatementsHandle statements,
        IntPtr positional,
        out ulong out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_execute_stmt_named")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_execute_stmt_named(
        StatementsHandle statements,
        IntPtr named,
        out ulong out_rows,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_run_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_run_stmt(StatementsHandle statements, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_run_stmt_positional")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_run_stmt_positional(
        StatementsHandle statements,
        IntPtr positional,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_run_stmt_named")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_run_stmt_named(
        StatementsHandle statements,
        IntPtr named,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_reset_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_reset_stmt(StatementsHandle statements, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_finalize_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_finalize_stmt(StatementsHandle statements, out IntPtr out_err_msg);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_stmt(IntPtr statements);
}
