namespace LibSql.Bindings;

public partial class Connection : IDisposable
{
    internal ConnectionHandle _connection;

    internal Connection(ConnectionHandle connection)
    {
        _connection = connection;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    public async Task LoadExtension(string path, string? entry_point = null)
    {
        await Task.Run(() =>
        {
            IntPtr err;
            var errorCode = libsql_load_extension(_connection, path, entry_point, out err);
            Utils.HandleError(errorCode, err);
        });
    }

    public async Task Reset()
    {
        await Task.Run(() =>
        {
            IntPtr err;
            var errorCode = libsql_reset(_connection, out err);
            Utils.HandleError(errorCode, err);
        });
    }

    public async Task Disconnect()
    {
        await Task.Run(() =>
        {
            _connection.Close();
        });
    }

    public async Task<Transaction> Transaction(TransactionType transactionType)
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            var transaction = nint.Zero;
            var errorCode = libsql_transaction_with_behavior(
                _connection,
                out transaction,
                (int)transactionType,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Transaction(new TransactionHandle(transaction));
        });
    }

    public async Task<Statements> Prepare(string sql)
    {
        return await Task.Run(() =>
        {
            nint err = nint.Zero;
            nint statements = nint.Zero;
            var errorCode = libsql_prepare(_connection, sql, out statements, out err);
            Utils.HandleError(errorCode, err);

            return new Statements(new StatementsHandle(statements));
        });
    }

    public async Task<Rows> Query(string sql)
    {
        return await Task.Run(() =>
        {
            nint err = nint.Zero;
            nint rows = nint.Zero;
            var errorCode = libsql_query(_connection, sql, out rows, out err);
            Utils.HandleError(errorCode, err);
            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<Rows> Query(string sql, params object?[] positionalValues)
    {
        return await Task.Run(() =>
        {
            var posVals = new PositionalValues(positionalValues);
            nint rows = nint.Zero;
            IntPtr err;
            var errorCode = libsql_query_positional(
                _connection,
                sql,
                posVals._positionalValues,
                out rows,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<Rows> Query(string sql, params (string, object?)[] namedValues)
    {
        return await Task.Run(() =>
        {
            var namedVals = new NamedValues(namedValues);
            nint rows = nint.Zero;
            IntPtr err;
            var errorCode = libsql_query_named(
                _connection,
                sql,
                namedVals._namedValues,
                out rows,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<ulong> Execute(string sql)
    {
        return await Task.Run(() =>
        {
            nint err = nint.Zero;
            ulong rowsChange = 0;
            var errorCode = libsql_execute_none(_connection, sql, out rowsChange, out err);
            Utils.HandleError(errorCode, err);
            return rowsChange;
        });
    }

    public async Task<ulong> Execute(string sql, params object?[] positionalValues)
    {
        return await Task.Run(() =>
        {
            var posVals = new PositionalValues(positionalValues);
            ulong rowChanges = 0;
            IntPtr err;
            var errorCode = libsql_execute_positional(
                _connection,
                sql,
                posVals._positionalValues,
                out rowChanges,
                out err
            );
            Utils.HandleError(errorCode, err);
            return rowChanges;
        });
    }

    public async Task<ulong> Execute(string sql, params (string, object?)[] namedValues)
    {
        return await Task.Run(() =>
        {
            var namedVals = new NamedValues(namedValues);
            ulong rowChanges = 0;
            IntPtr err;
            var errorCode = libsql_execute_named(
                _connection,
                sql,
                namedVals._namedValues,
                out rowChanges,
                out err
            );
            Utils.HandleError(errorCode, err);
            return rowChanges;
        });
    }

    public ulong Changes()
    {
        return libsql_changes(_connection);
    }

    public long LastInsertRowId()
    {
        return libsql_last_insert_rowid(_connection);
    }
}
