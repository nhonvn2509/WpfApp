using Microsoft.Data.SqlClient;
using WpfApp.Models.Database;

namespace WpfApp.Services.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();

    bool IsWellFormedConnectionString(string? value);

    Task<ConnectionProbe> ProbeAsync(string connectionString, CancellationToken cancellationToken = default);
}
