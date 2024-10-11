namespace LibSql.Bindings;

public enum ColumnType
{
    INT = 1,
    FLOAT = 2,
    TEXT = 3,
    BLOB = 4,
    NULL = 5,
}

public partial class Rows : IDisposable
{
    internal RowsHandle _rows;

    internal Rows(RowsHandle rows)
    {
        _rows = rows;
    }

    // Return rows until null or error
    public async Task<Row?> GetNextRow()
    {
        return await Task.Run(() =>
        {
            IntPtr err;
            IntPtr row;
            var errorCode = libsql_next_row(_rows, out row, out err);
            Utils.HandleError(errorCode, err);

            if (row == IntPtr.Zero)
                return null;

            return new Row(new RowHandle(row));
        });
    }

    public int ColumnCount()
    {
        return libsql_column_count(_rows);
    }

    public string ColumnName(int col)
    {
        IntPtr err;
        IntPtr namePtr;
        var errorCode = libsql_column_name(_rows, col, out namePtr, out err);
        Utils.HandleError(errorCode, err);
        return Utils.IntoStringAndFree(namePtr);
    }

    public ColumnType ColumnType(Row row, int col)
    {
        IntPtr err;
        int type;
        var errorCode = libsql_column_type(
            _rows,
            row._row.DangerousGetHandle(),
            col,
            out type,
            out err
        );
        Utils.HandleError(errorCode, err);
        if (Enum.IsDefined(typeof(ColumnType), type))
        {
            return (ColumnType)type;
        }
        return Bindings.ColumnType.NULL;
    }

    public void Dispose()
    {
        _rows.Dispose();
    }
}
