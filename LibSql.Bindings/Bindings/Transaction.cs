namespace LibSql.Bindings;

public enum TransactionType
{
    DEFERRED = 1,
    IMMEDIATE = 2,
    EXCLUSIVE = 3,
    READONLY = 4,
}

public partial class Transaction : IDisposable
{
    internal TransactionHandle _transactionHandle;

    internal Transaction(TransactionHandle transactionHandle)
    {
        _transactionHandle = transactionHandle;
    }

    public void Dispose()
    {
        _transactionHandle.Dispose();
    }

    // Consume transaction
    public async Task Commit()
    {
        await Task.Run(() =>
        {
            var err = nint.Zero;
            var errorCode = libsql_commit_transaction(_transactionHandle, out err);
            Utils.HandleError(errorCode, err);
            _transactionHandle.Close();
        });
    }

    // Consume transaction
    public async Task Rollback()
    {
        await Task.Run(() =>
        {
            var err = nint.Zero;
            var errorCode = libsql_rollback_transaction(_transactionHandle, out err);
            Utils.HandleError(errorCode, err);
            _transactionHandle.Close();
        });
    }

    public Connection RetrieveConnection()
    {
        var connection = nint.Zero;
        libsql_connection_transaction(_transactionHandle, out connection);
        // This connection should be the same as the one that create this Transaction? Anyways the disposal is
        // handle by the Transaction type, so this needs better design.
        return new Connection(new ConnectionHandle(connection, false));
    }
}
