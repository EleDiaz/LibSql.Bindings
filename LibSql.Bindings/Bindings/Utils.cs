using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibSql.Bindings;

internal abstract class LibSqlSafeHandle : SafeHandle
{
    public LibSqlSafeHandle(nint ptr)
        : base(nint.Zero, true)
    {
        SetHandle(ptr);
    }

    public LibSqlSafeHandle()
        : base(nint.Zero, true) { }

    public override bool IsInvalid => nint.Zero == handle;
}

internal partial class Utils
{
    internal const string __DllName = "libsql_cs";

    [LibraryImport(__DllName, EntryPoint = "libsql_free_string")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_string(IntPtr str);

    internal static void HandleError(int errorCode, IntPtr errMsg)
    {
        if (errorCode != 0 && errMsg != IntPtr.Zero)
        {
            var error = Marshal.PtrToStringAnsi(errMsg);
            Utils.libsql_free_string(errMsg);
            throw new LibSqlException(error);
        }
    }

    internal static string IntoStringAndFree(IntPtr str)
    {
        var name = Marshal.PtrToStringAnsi(str);
        Utils.libsql_free_string(str);
        return name ?? ""; // ? Should we throw an exception?
    }
}

internal class PositionalValuesHandle : LibSqlSafeHandle
{
    public PositionalValuesHandle()
        : base()
    {
        PositionalValues.libsql_make_positional_values(out var posValuesPtr);
        SetHandle(posValuesPtr);
    }

    protected override bool ReleaseHandle()
    {
        PositionalValues.libsql_free_positional_values(handle);
        return true;
    }
}

internal partial class PositionalValues
{
    internal PositionalValuesHandle _positionalValues = new();

    internal PositionalValues() { }

    internal PositionalValues(Value?[] positionalValues)
    {
        for (int i = 0; i < positionalValues.Length; i++)
        {
            Set(positionalValues[i], (uint)i);
        }
    }

    internal PositionalValues(object?[] positionalValues)
    {
        for (int i = 0; i < positionalValues.Length; i++)
        {
            Set(Value.FromObject(positionalValues[i]), (uint)i);
        }
    }

    public unsafe void Set(Value? value, uint idx)
    {
        IntPtr err;
        var handleBlob = (byte[] blob, out IntPtr err) =>
        {
            fixed (byte* blobRaw = blob)
            {
                return libsql_positional_bind_blob(
                    _positionalValues,
                    idx,
                    (IntPtr)blobRaw,
                    blob.Length,
                    out err
                );
            }
        };
        var errorCode = value switch
        {
            IntValue intValue => libsql_positional_bind_int(
                _positionalValues,
                idx,
                intValue.Value,
                out err
            ),
            StringValue stringValue => libsql_positional_bind_string(
                _positionalValues,
                idx,
                stringValue.Value,
                out err
            ),
            FloatValue floatValue => libsql_positional_bind_float(
                _positionalValues,
                idx,
                floatValue.Value,
                out err
            ),
            BlobValue blobValue => handleBlob(blobValue.Value, out err),
            null => libsql_positional_bind_null(_positionalValues, idx, out err),
            _ => throw new LibSqlException("Value not considered"),
        };
        Utils.HandleError(errorCode, err);
    }

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_make_positional_values")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_make_positional_values(out IntPtr pos_values);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_positional_values")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_positional_values(IntPtr pos_values);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_positional_bind_int")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_positional_bind_int(
        PositionalValuesHandle pos_values,
        uint idx,
        long value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_positional_bind_float")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_positional_bind_float(
        PositionalValuesHandle pos_values,
        uint idx,
        double value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_positional_bind_null")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_positional_bind_null(
        PositionalValuesHandle pos_values,
        uint idx,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_positional_bind_string",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_positional_bind_string(
        PositionalValuesHandle pos_values,
        uint idx,
        string value,
        out IntPtr out_err_msg
    );

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_positional_bind_blob")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_positional_bind_blob(
        PositionalValuesHandle pos_values,
        uint idx,
        IntPtr value,
        int value_len,
        out IntPtr out_err_msg
    );
}

internal class NamedValuesHandle : LibSqlSafeHandle
{
    public NamedValuesHandle()
        : base()
    {
        NamedValues.libsql_make_namedvalues(out var namedValuesPtr);
        SetHandle(namedValuesPtr);
    }

    protected override bool ReleaseHandle()
    {
        NamedValues.libsql_free_namedvalues(handle);
        return true;
    }
}

internal partial class NamedValues
{
    internal NamedValuesHandle _namedValuesHandle = new();

    internal NamedValues() { }

    internal NamedValues((string, Value?)[] namedValues)
    {
        for (int i = 0; i < namedValues.Length; i++)
        {
            Set(namedValues[i].Item1, namedValues[i].Item2);
        }
    }

    internal NamedValues((string, object?)[] namedValues)
    {
        for (int i = 0; i < namedValues.Length; i++)
        {
            Set(namedValues[i].Item1, Value.FromObject(namedValues[i].Item2));
        }
    }

    public unsafe void Set(string name, Value? value)
    {
        IntPtr err;
        var handleBlob = (byte[] blob, out IntPtr err) =>
        {
            fixed (byte* blobRaw = blob)
            {
                return libsql_named_bind_blob(
                    _namedValuesHandle,
                    name,
                    (IntPtr)blobRaw,
                    blob.Length,
                    out err
                );
            }
        };

        var errorCode = value switch
        {
            IntValue intValue => libsql_named_bind_int(
                _namedValuesHandle,
                name,
                intValue.Value,
                out err
            ),
            StringValue stringValue => libsql_named_bind_string(
                _namedValuesHandle,
                name,
                stringValue.Value,
                out err
            ),
            FloatValue floatValue => libsql_named_bind_float(
                _namedValuesHandle,
                name,
                floatValue.Value,
                out err
            ),
            BlobValue blobValue => handleBlob(blobValue.Value, out err),
            null => libsql_named_bind_null(_namedValuesHandle, name, out err),
            _ => throw new LibSqlException("Value not considered"),
        };
        Utils.HandleError(errorCode, err);
    }

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_make_namedvalues")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_make_namedvalues(out IntPtr named_vals);

    [LibraryImport(Utils.__DllName, EntryPoint = "libsql_free_namedvalues")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void libsql_free_namedvalues(IntPtr named_vals);

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_named_bind_int",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_named_bind_int(
        NamedValuesHandle named_vals,
        string name,
        long value,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_named_bind_float",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_named_bind_float(
        NamedValuesHandle named_vals,
        string name,
        double value,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_named_bind_null",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_named_bind_null(
        NamedValuesHandle named_vals,
        string name,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_named_bind_string",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_named_bind_string(
        NamedValuesHandle named_vals,
        string name,
        string value,
        out IntPtr out_err_msg
    );

    [LibraryImport(
        Utils.__DllName,
        EntryPoint = "libsql_named_bind_blob",
        StringMarshalling = StringMarshalling.Utf8
    )]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int libsql_named_bind_blob(
        NamedValuesHandle named_vals,
        string name,
        IntPtr value,
        int value_len,
        out IntPtr out_err_msg
    );
}

[Serializable]
public class LibSqlException : Exception
{
    public LibSqlException() { }

    public LibSqlException(string? message)
        : base(message ?? "LibSql Bindings: error + marshalling") { }

    public LibSqlException(string? message, Exception? innerException)
        : base(message ?? "LibSql Bindings: error + marshalling", innerException) { }
}
