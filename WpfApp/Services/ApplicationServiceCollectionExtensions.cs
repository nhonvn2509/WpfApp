using Microsoft.Extensions.DependencyInjection;
using WpfApp.Presentation;
using WpfApp.Services.Appearance;
using WpfApp.Services.Data;
using WpfApp.Services.Files;
using WpfApp.Services.Settings;
using WpfApp.Services.Tables;
using WpfApp.ViewModel;
using WpfApp.ViewModel.Settings;
using WpfApp.ViewModel.Tables;
using WpfApp.ViewModel.Workflows;

namespace WpfApp.Services;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUserSettings, JsonUserSettings>();
        services.AddSingleton<JsonAppSettings>();
        services.AddSingleton<IAppSettings>(provider => new UserFirstAppSettings(
            provider.GetRequiredService<IUserSettings>(),
            provider.GetRequiredService<JsonAppSettings>()));
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IAppThemeService, AppThemeService>();

    services.AddSingleton<ITableRowCountProvider, TableRowCountProvider>();

    services.AddSingleton<IDatabaseInsightsService, DatabaseInsightsService>();
        services.AddSingleton<IDataBootstrapper, DataBootstrapper>();
        services.AddSingleton<IFilePathResolver, FilePathResolver>();
        services.AddSingleton<IChainsLoadService, ChainsLoadService>();
        services.AddSingleton<ISessionsLoadService, SessionsLoadService>();
        services.AddSingleton<IContactsLoadService, ContactsLoadService>();
        services.AddSingleton<IFilesLoadService, FilesLoadService>();
        services.AddSingleton<IFileDetailRepository, FileDetailRepository>();
        services.AddSingleton<IFileDetailReportBuilder, FileDetailReportBuilder>();
        services.AddSingleton<IFileDetailService, FileDetailService>();
        services.AddSingleton<IWordDocumentBuilder, WordDocumentBuilder>();
        services.AddSingleton<IWordExportService, WordExportService>();
        services.AddSingleton<IFileExportService, FileExportService>();

        services.AddSingleton<IDialogService>(provider => new WpfDialogService(provider));
        services.AddScoped<ChainsTableViewModel>();
        services.AddScoped<SessionsTableViewModel>();
        services.AddScoped<ContactsTableViewModel>();
        services.AddScoped<FileExtensionsViewModel>();
        services.AddScoped<SessionFilesViewModel>();
        services.AddScoped<IChainExploitationCoordinator, ChainExploitationCoordinator>();
        services.AddScoped<MainWindowViewModel>();
        services.AddSingleton<SaveToCatalogService>();
        services.AddSingleton<SaveToPreferencesStore>();
        services.AddSingleton<SaveToFileCollector>();
        services.AddSingleton<SaveToCopyService>();
        services.AddTransient<SaveSessionsViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
