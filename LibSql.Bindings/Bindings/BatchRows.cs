using System.Collections;

namespace LibSql.Bindings;

public partial class BatchRows : IEnumerable<Rows?>
{
    internal BatchRowsHandle _batchRowsHandle;

    internal BatchRows(BatchRowsHandle batchRowsHandle)
    {
        _batchRowsHandle = batchRowsHandle;
    }

    public IEnumerator<Rows?> GetEnumerator()
    {
        var status = 0;
        while (status == 0)
        {
            var out_rows = nint.Zero;
            status = libsql_next_stmt_row_batchrows(_batchRowsHandle, out out_rows);

            if (out_rows == nint.Zero)
            {
                yield return null;
            }
            else
            {
                yield return new Rows(new RowsHandle(out_rows));
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
