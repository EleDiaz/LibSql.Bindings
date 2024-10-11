using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DbConfigRaw
{
    public IntPtr DbPath;
    public IntPtr PrimaryUrl;
    public IntPtr AuthToken;
    public byte ReadYourWrites;
    public IntPtr EncryptionKey;
    public int SyncInterval;
    public byte WithWebpki;
}

internal class DatabaseHandle : SafeHandle
{
    public DatabaseHandle(nint ptr)
        : base(IntPtr.Zero, true)
    {
        SetHandle(ptr);
    }

    public override bool IsInvalid => IntPtr.Zero == handle;

    protected override bool ReleaseHandle()
    {
        Database.libsql_close(handle);
        return true;
    }
}

public partial class Database
{
    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_sync",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_sync(
        DatabaseHandle db,
        out Replicated out_replicated,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_open_file",
        StringMarshalling = StringMarshalling.Utf8
    )]
    internal static partial int libsql_open_file(
        string url,
        out IntPtr out_db,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_open_remote_internal",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_open_remote(
        string url,
        string auth_token,
        [MarshalAs(UnmanagedType.U1)] bool with_webpki,
        out IntPtr out_db,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_open_sync_with_config")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_open_sync_with_config(
        DbConfigRaw config,
        out IntPtr out_db,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_close")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_close(IntPtr db);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_connect")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_connect(
        DatabaseHandle db,
        out IntPtr out_conn,
        out IntPtr out_err_msg
    );
}
