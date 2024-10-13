using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LibSql.Bindings.Test;

public class DatabaseContainer : IAsyncDisposable
{
    public IContainer libsqlContainer;

    public DatabaseContainer()
    {
        libsqlContainer = new ContainerBuilder()
            .WithImage("ghcr.io/tursodatabase/libsql-server:latest")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(8080))
            )
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        await libsqlContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
