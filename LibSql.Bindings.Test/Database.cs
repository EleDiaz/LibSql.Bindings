using LibSql.Bindings;

namespace LibSql.Bindings.Test;

public class DatabaseTest : IAsyncLifetime, IClassFixture<DatabaseContainer>
{
    private readonly DatabaseContainer _databaseContainer;
    public Database memoryDb = null!;
    public Database remoteDb = null!;
    public Database replicaDb = null!;
    public Connection memoryConnection = null!;

    public DatabaseTest(DatabaseContainer databaseContainer)
    {
        _databaseContainer = databaseContainer;
    }

    public async Task InitializeAsync()
    {
        await _databaseContainer.libsqlContainer.StartAsync();
        var url =
            $"http://{_databaseContainer.libsqlContainer.Hostname}:{_databaseContainer.libsqlContainer.GetMappedPublicPort(8080)}";

        memoryDb = await Database.OpenLocalFile(":memory:");
        memoryConnection = memoryDb.Connect();

        remoteDb = await Database.OpenRemote(url, "");

        replicaDb = await Database.OpenSync("test-remote.db", url, "", true);
    }

    public Task DisposeAsync()
    {
        return Task.Run(() => { });
    }

    [Fact]
    public async Task OpenRemoteReplica()
    {
        Assert.NotNull(replicaDb);

        var connection = replicaDb.Connect();
        var rows = await connection.Query("PRAGMA database_list;");

        Assert.Equal(3, rows.ColumnCount());

        var created_rows = await connection.Execute(
            "CREATE TABLE IF NOT EXISTS test_replicated (azar)"
        );
        Assert.Equal((ulong)0, created_rows);

        await connection.Execute("INSERT INTO test_replicated VALUES ('clear')");

        var replicated = await replicaDb.Sync();

        // Assert.Equal(1, replicated.FramesSynced);
        Assert.Equal(2, replicated.FrameNo);

        // await memoryConnection.Execute("DROP TABLE test_replicated");
    }

    [Fact]
    public async Task OpenLocal()
    {
        var db = await Database.OpenLocalFile("test.db");
        Assert.NotNull(db);

        var connection = db.Connect();
        var rows = await connection.Query("PRAGMA database_list;");

        Assert.Equal(3, rows.ColumnCount());
    }

    [Fact]
    public async Task OpenRemote()
    {
        var connection = remoteDb.Connect();
        var rows = await connection.Query("PRAGMA database_list;");

        Assert.Equal(3, rows.ColumnCount());
    }

    [Fact]
    public async Task LocalPositional()
    {
        var created_rows = await memoryConnection.Execute(
            "CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT, height REAL, hobby TEXT)"
        );
        Assert.Equal((ulong)0, created_rows);

        var inserted_rows = await memoryConnection.Execute(
            "INSERT INTO users VALUES (?, ?, ?, ?)",
            95,
            null,
            1.78,
            "none"
        );
        Assert.Equal((ulong)1, inserted_rows);

        var rows = await memoryConnection.Query(
            "SELECT id, name, height, hobby FROM users WHERE id == ?",
            new IntValue(95)
        );

        var row = await rows.GetNextRow();

        Assert.Equal(95, row!.GetInt(0));
    }

    [Fact]
    public async Task LocalNamed()
    {
        var created_rows = await memoryConnection.Execute(
            "CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT, height REAL, hobby TEXT)"
        );
        Assert.Equal((ulong)0, created_rows);

        StringValue test = "";

        var inserted_rows = await memoryConnection.Execute(
            "INSERT INTO users VALUES (@id, @name, @h, @hobby)",
            ("@hobby", 4311),
            ("@name", "hello")
        );
        Assert.Equal((ulong)1, inserted_rows);

        var rows = await memoryConnection.Query(
            "SELECT id, name, height, hobby FROM users WHERE hobby == ?",
            4311
        );

        var row = await rows.GetNextRow();

        Assert.Equal("hello", row!.GetString(1));
    }

    [Fact]
    public async Task LocalStatements()
    {
        var created_rows = await memoryConnection.Execute("CREATE TABLE things (name)");
        Assert.Equal((ulong)0, created_rows);

        var insert_statement = await memoryConnection.Prepare("INSERT INTO things VALUES (@name)");

        await insert_statement.Run(("@name", "hi"));

        await insert_statement.Run(("@name", "tes")); // doesnt get apply, keeps using the last parameters binded
        await insert_statement.Run(); // the same keeps using the first parameters binded

        // https://www.sqlite.org/c3ref/reset.html
        // The reset parameter so we can change the parameters
        insert_statement.Reset();

        await insert_statement.Run(("@name", "tes"));
        await insert_statement.Run();

        var rows = await memoryConnection.Query("SELECT COUNT(*) FROM things WHERE name='hi'");
        var row = await rows.GetNextRow();

        Assert.Equal(3, row!.GetInt(0));

        // var stmt = await memoryConnection.Prepare("SELECT * FROM users");
        // Assert.NotNull(stmt);
        //
        // await stmt.Query();
    }
}
