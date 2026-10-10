using Microsoft.Data.SqlClient;

namespace WpfApp.Services.Data;

public interface ITableRowCountProvider
{
    Task<long> GetRowCountAsync(string qualifiedTableName, CancellationToken cancellationToken = default);
}
