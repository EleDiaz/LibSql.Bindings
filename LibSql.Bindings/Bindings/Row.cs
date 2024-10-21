using System.Runtime.InteropServices;

namespace LibSql.Bindings;

public partial class Row
{
    internal RowHandle _row;

    internal Row(RowHandle row)
    {
        _row = row;
    }

    public void Dispose()
    {
        _row.Dispose();
    }

    public string? GetString(int col)
    {
        IntPtr err;
        IntPtr val;
        var errorCode = libsql_get_string(_row, col, out val, out err);
        Utils.HandleError(errorCode, err);
        var str = Marshal.PtrToStringUTF8(val);
        Utils.libsql_free_string(val);
        return str;
    }

    public long GetInt(int col)
    {
        IntPtr err;
        long val;
        var errorCode = libsql_get_int(_row, col, out val, out err);
        Utils.HandleError(errorCode, err);
        return val;
    }

    public double GetDouble(int col)
    {
        IntPtr err;
        double val;
        var errorCode = libsql_get_float(_row, col, out val, out err);
        Utils.HandleError(errorCode, err);
        return val;
    }

    public Blob GetBlob(int col)
    {
        IntPtr err;
        BlobRaw val;
        var errorCode = libsql_get_blob(_row, col, out val, out err);
        Utils.HandleError(errorCode, err);
        return new Blob(val);
    }
}
