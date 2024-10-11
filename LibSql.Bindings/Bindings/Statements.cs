namespace LibSql.Bindings;

// It lacks of some methods which aren't essential
public partial class Statements : IDisposable
{
    internal StatementsHandle _statements;

    internal Statements(StatementsHandle handle)
    {
        _statements = handle;
    }

    public void Dispose()
    {
        _statements.Dispose();
    }

    public async Task<Rows> Query()
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            var rows = nint.Zero;
            var errorCode = libsql_query_stmt(_statements, out rows, out err);
            Utils.HandleError(errorCode, err);

            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<Rows> Query(params object?[] positionalValues)
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            var rows = nint.Zero;
            var posVals = new PositionalValues(positionalValues);
            var errorCode = libsql_query_stmt_positional(
                _statements,
                posVals._positionalValues,
                out rows,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<Rows> Query(params (string, object?)[] namedValues)
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            var rows = nint.Zero;
            var namedVals = new NamedValues(namedValues);
            var errorCode = libsql_query_stmt_named(
                _statements,
                namedVals._namedValues,
                out rows,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Rows(new RowsHandle(rows));
        });
    }

    public async Task<ulong> Execute()
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            ulong rows_changed;
            var errorCode = libsql_execute_stmt(_statements, out rows_changed, out err);
            Utils.HandleError(errorCode, err);

            return rows_changed;
        });
    }

    public async Task<ulong> Execute(params object?[] positionalValues)
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            ulong rows_changed;
            var posVals = new PositionalValues(positionalValues);
            var errorCode = libsql_execute_stmt_positional(
                _statements,
                posVals._positionalValues,
                out rows_changed,
                out err
            );
            Utils.HandleError(errorCode, err);
            return rows_changed;
        });
    }

    public async Task<ulong> Execute(params (string, object?)[] namedValues)
    {
        return await Task.Run(() =>
        {
            var err = nint.Zero;
            ulong rows_changed;
            var namedVals = new NamedValues(namedValues);
            var errorCode = libsql_execute_stmt_named(
                _statements,
                namedVals._namedValues,
                out rows_changed,
                out err
            );
            Utils.HandleError(errorCode, err);
            return rows_changed;
        });
    }

    public async Task Run()
    {
        await Task.Run(() =>
        {
            var err = nint.Zero;
            var errorCode = libsql_run_stmt(_statements, out err);
            Utils.HandleError(errorCode, err);
        });
    }

    public async Task Run(params object?[] positionalValues)
    {
        await Task.Run(() =>
        {
            var err = nint.Zero;
            var posVals = new PositionalValues(positionalValues);
            var errorCode = libsql_run_stmt_positional(
                _statements,
                posVals._positionalValues,
                out err
            );
            Utils.HandleError(errorCode, err);
        });
    }

    public async Task Run(params (string, object?)[] namedValues)
    {
        await Task.Run(() =>
        {
            var err = nint.Zero;
            var namedVals = new NamedValues(namedValues);
            var errorCode = libsql_run_stmt_named(_statements, namedVals._namedValues, out err);
            Utils.HandleError(errorCode, err);
        });
    }

    //
    public void FinalizeStatements()
    {
        var err = nint.Zero;
        var errorCode = libsql_finalize_stmt(_statements, out err);
        Utils.HandleError(errorCode, err);
    }

    public void Reset()
    {
        var err = nint.Zero;
        var errorCode = libsql_reset_stmt(_statements, out err);
        Utils.HandleError(errorCode, err);
    }
}
