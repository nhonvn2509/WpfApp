namespace WpfApp.Services;

public interface IDataBootstrapper
{
    Task InitializeAsync(Action<string> updateStatus);
}
