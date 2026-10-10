namespace WpfApp.Models.Settings;

public sealed record DatabaseTarget(string Name, string ConnectionString)
{
    public override string ToString() => Name;
}

public static class KnownDatabases
{
    public const string Db01Name = "db-01";

    public const string AspaTa01Name = "ASPA-TA-01";

    public const string LocalName = "LOCAL";

    public const string Db01 =
        "Data Source=192.161.2.91;Initial Catalog=DeltaBat;Integrated Security=True;" +
        "Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;" +
        "Encrypt=True;TrustServerCertificate=True;Application Name=DeltaBat;Command Timeout=60";

    public const string AspaTa01 =
        "Data Source=192.168.2.95;Initial Catalog=DeltaBat;Integrated Security=True;" +
        "Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;" +
        "Encrypt=True;TrustServerCertificate=True;Application Name=DeltaBat;Command Timeout=60";

    public const string Local =
        "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=DeltaBat;Integrated Security=True;" +
        "Persist Security Info=False;Pooling=True;MultipleActiveResultSets=False;" +
        "Encrypt=True;TrustServerCertificate=True;Application Name=DeltaBat;Command Timeout=60";

    public static IReadOnlyList<DatabaseTarget> All { get; } =
    [
        new(Db01Name, Db01),
        new(AspaTa01Name, AspaTa01),
        new(LocalName, Local)
    ];
}
