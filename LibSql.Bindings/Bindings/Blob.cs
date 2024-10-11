using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct BlobRaw
{
    internal IntPtr ptr;
    internal int len;

    internal byte ReadByte(int index)
    {
        if (index < 0 || index >= len)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index out of range.");
        }
        return Marshal.ReadByte(ptr, index);
    }

    internal Span<byte> GetSpan() {
        return new Span<byte>((byte*)ptr, len);
    }
}

public partial class Blob : IDisposable
{
    internal BlobRaw _blob;

    private bool disposed = false;

    internal Blob(BlobRaw blob) {
        _blob = blob;
    }

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_blob")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_blob(BlobRaw b);

    public void Dispose()
    {
        if (this.disposed)
            return;

        if (_blob.ptr != IntPtr.Zero)
        {
            libsql_free_blob(_blob);
        }

        GC.SuppressFinalize(this);
        disposed = true;
    }

    ~Blob()
    {
        Dispose();
    }
}
