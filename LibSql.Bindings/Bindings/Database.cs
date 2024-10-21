using System.Runtime.InteropServices;

namespace LibSql.Bindings;

public struct DbConfig
{
    public string? DbPath;
    public string? PrimaryUrl;
    public string? AuthToken;
    public bool ReadYourWrites;
    public string? EncryptionKey;
    public int SyncInterval;
    public bool WithWebpki;

    internal DbConfigRaw GetRaw()
    {
        return new DbConfigRaw
        {
            DbPath = Marshal.StringToCoTaskMemUTF8(DbPath),
            PrimaryUrl = Marshal.StringToCoTaskMemUTF8(PrimaryUrl),
            AuthToken = Marshal.StringToCoTaskMemUTF8(AuthToken),
            ReadYourWrites = (byte)(ReadYourWrites ? 1 : 0),
            EncryptionKey = Marshal.StringToCoTaskMemUTF8(EncryptionKey),
            SyncInterval = SyncInterval,
            WithWebpki = (byte)(WithWebpki ? 1 : 0),
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Replicated
{
    public int FrameNo;
    public int FramesSynced;
}

// We are specifying the call conv, due to windows defautl to stdconv, havent check

public partial class Database : IDisposable
{
    internal DatabaseHandle _database;

    private Database(DatabaseHandle database)
    {
        _database = database;
    }

    public void Dispose()
    {
        _database.Dispose();
    }

    public async Task<Replicated> Sync()
    {
        return await Task.Run(() =>
        {
            IntPtr err;
            Replicated replicate;
            var errorCode = libsql_sync(_database, out replicate, out err);
            Utils.HandleError(errorCode, err);

            return replicate;
        });
    }

    public static async Task<Database> OpenSync(
        string dbPath,
        string primaryUrl,
        string authToken,
        bool readYourWrites,
        string? encryptionKey = null
    )
    {
        return await OpenWithConfig(
            new DbConfig
            {
                DbPath = dbPath,
                PrimaryUrl = primaryUrl,
                AuthToken = authToken,
                ReadYourWrites = readYourWrites,
                EncryptionKey = encryptionKey,
                SyncInterval = 0,
                WithWebpki = false,
            }
        );
    }

    public static async Task<Database> OpenSyncWithWebpki(
        string dbPath,
        string primaryUrl,
        string authToken,
        bool readYourWrites,
        string encryptionKey
    )
    {
        return await OpenWithConfig(
            new DbConfig
            {
                DbPath = dbPath,
                PrimaryUrl = primaryUrl,
                AuthToken = authToken,
                ReadYourWrites = readYourWrites,
                EncryptionKey = encryptionKey,
                SyncInterval = 0,
                WithWebpki = true,
            }
        );
    }

    public static async Task<Database> OpenLocalFile(string file)
    {
        return await Task.Run(() =>
        {
            IntPtr db;
            IntPtr err;
            var errorCode = libsql_open_file(file, out db, out err);
            Utils.HandleError(errorCode, err);
            return new Database(new DatabaseHandle(db));
        });
    }

    public static async Task<Database> OpenRemote(
        string url,
        string authToken,
        bool withWebpki = false
    )
    {
        return await Task.Run(() =>
        {
            IntPtr db;
            IntPtr err;
            var errorCode = libsql_open_remote(
                url,
                authToken,
                withWebpki,
                out db,
                out err
            );
            Utils.HandleError(errorCode, err);
            return new Database(new DatabaseHandle(db));
        });
    }

    public static async Task<Database> OpenWithConfig(DbConfig config)
    {
        return await Task.Run(() =>
        {
            var configRaw = config.GetRaw();
            IntPtr db;
            IntPtr err;
            var errorCode = libsql_open_sync_with_config(configRaw, out db, out err);
            Marshal.FreeCoTaskMem(configRaw.DbPath);
            Marshal.FreeCoTaskMem(configRaw.PrimaryUrl);
            Marshal.FreeCoTaskMem(configRaw.AuthToken);
            Marshal.FreeCoTaskMem(configRaw.EncryptionKey);
            Utils.HandleError(errorCode, err);
            return new Database(new DatabaseHandle(db));
        });
    }

    public Connection Connect()
    {
        IntPtr conn;
        IntPtr err;
        var errorCode = libsql_connect(_database, out conn, out err);
        Utils.HandleError(errorCode, err);
        return new Connection(new ConnectionHandle(conn));
    }

    private void Close()
    {
        _database.Close();
    }
}
