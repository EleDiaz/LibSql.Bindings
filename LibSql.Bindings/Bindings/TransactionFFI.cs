using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal class TransactionHandle : LibSqlSafeHandle
{
    public TransactionHandle(nint ptr)
        : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        Transaction.libsql_free_transaction(handle);
        return true;
    }
}

public partial class Transaction
{
    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_stmt")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_transaction(IntPtr transaction);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_rollback_transaction")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_rollback_transaction(
        TransactionHandle transaction,
        out IntPtr out_msg_err
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_commit_transaction")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_commit_transaction(
        TransactionHandle transaction,
        out IntPtr out_msg_err
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_connection_transaction")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_connection_transaction(
        TransactionHandle transaction,
        out IntPtr connection
    );
}
