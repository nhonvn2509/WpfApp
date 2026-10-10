namespace WpfApp.Models.Database;

public sealed record ConnectionProbe(
    string ServerName,
    string DatabaseName,
    string ProductVersion,
    string Edition,
    double ElapsedMilliseconds);
