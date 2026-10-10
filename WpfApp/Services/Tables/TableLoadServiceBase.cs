using Microsoft.Data.SqlClient;
using WpfApp.Services.Data;

namespace WpfApp.Services.Tables;

public abstract class SqlTableLoadService
{
    protected SqlTableLoadService(ISqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ConnectionFactory = connectionFactory;
    }

    protected ISqlConnectionFactory ConnectionFactory { get; }

    protected async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = ConnectionFactory.Create();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
