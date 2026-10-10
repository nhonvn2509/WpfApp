using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Threading;
using HandyControl.Tools;
using Microsoft.Extensions.DependencyInjection;
using WpfApp.Presentation;
using WpfApp.Services;
using WpfApp.Services.Appearance;
using WpfApp.ViewModel;
using WpfApp.Views;

namespace WpfApp
{
    public partial class App : Application
    {
        private ServiceProvider? _services;
        private IServiceScope? _scope;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterGlobalExceptionHandlers();
            ApplyVietnameseLanguage();
            _ = StartAsync();
        }

        /// <summary>
        /// Bắt mọi lỗi không xử lý được để ứng dụng không bị đóng đột ngột.
        /// Trước đây thiếu handler này nên bất kỳ lỗi nào trên UI thread
        /// (ví dụ khi xem trước một tệp lỗi) đều tắt ngay ứng dụng không báo gì.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            // Lỗi trên UI thread (WPF dispatcher) — đây là nhóm hay gặp nhất khi xem trước tệp.
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Lỗi ở background thread không được await (async void, Task quên...).
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            // Task bị lỗi nhưng không ai chờ — tránh crash khi GC thu gom.
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            TryWriteCrashLog(e.Exception);
            ShowCrashMessage(e.Exception);
            // Đánh dấu đã xử lý để giữ ứng dụng tiếp tục chạy thay vì đóng.
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                TryWriteCrashLog(exception);
            }
        }

        private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            // Chỉ ghi log, không làm gì thêm để không ảnh hưởng tiến trình đang chạy.
            e.SetObserved();
        }

        private void TryWriteCrashLog(Exception exception)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "crash-log.txt");
                var payload = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{exception}\n\n";
                File.AppendAllText(path, payload);
            }
            catch (Exception)
            {
            }
        }

        private void ShowCrashMessage(Exception exception)
        {
            try
            {
                MessageBox.Show(
                    "Ứng dụng gặp lỗi không mong muốn nhưng vẫn tiếp tục chạy.\n\n" +
                    $"{exception.Message}\n\n" +
                    "Chi tiết đã được ghi vào crash-log.txt cạnh tệp thực thi.",
                    "DeltaBat - Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyVietnameseLanguage()
        {
            var culture = CultureInfo.GetCultureInfo("vi-VN");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var langType = typeof(ConfigHelper).Assembly.GetType("HandyControl.Properties.Langs.Lang");
            if (langType is not null)
            {
                const BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;
                langType.GetField("resourceMan", StaticPrivate)?.SetValue(
                    null,
                    new ResourceManager("HandyControl.Properties.Langs.Lang", typeof(App).Assembly));
                langType.GetField("resourceCulture", StaticPrivate)?.SetValue(null, culture);
            }

            ConfigHelper.Instance.SetLang("vi-VN");
        }

        private async Task StartAsync()
        {
            LoadingWindow? loading = null;
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _services = new ServiceCollection()
                    .AddApplicationServices()
                    .BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });
                _scope = _services.CreateScope();
                _services.GetRequiredService<IAppThemeService>().Load();
                if (!HasConnectionString())
                {
                    Shutdown(1);
                    return;
                }

                loading = new LoadingWindow();
                loading.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (!await ConnectAsync(loading))
                {
                    Shutdown(1);
                    return;
                }

                ShowMainWindow(loading);
            }
            catch (Exception exception)
            {
                loading?.Close();
                TryWriteStartupError(exception);
                MessageBox.Show(
                    $"Không thể khởi động ứng dụng.\n\n{exception.Message}",
                    "DeltaBat - Lỗi khởi động",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void TryWriteStartupError(Exception exception)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "startup-error.txt");
                File.WriteAllText(path, exception.ToString());
            }
            catch (Exception)
            {
            }
        }
        private async Task<bool> ConnectAsync(LoadingWindow loading)
        {
            var services = _services
                ?? throw new InvalidOperationException("Chưa khởi tạo dịch vụ ứng dụng.");
            while (true)
            {
                try
                {
                    await services.GetRequiredService<IDataBootstrapper>().InitializeAsync(loading.UpdateStatus);
                    return true;
                }
                catch (Exception ex)
                {
                    loading.UpdateStatus("Kết nối cơ sở dữ liệu thất bại");
                    var answer = MessageBox.Show(
                        $"Không thể kết nối cơ sở dữ liệu.\n\n{ex.Message}\n\nBạn có muốn thử lại?",
                        "DeltaBat - Lỗi kết nối",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes)
                    {
                        return false;
                    }
                    loading.UpdateStatus("Đang thử kết nối lại...");
                }
            }
        }
        private void ShowMainWindow(LoadingWindow loading)
        {
            var viewModel = _scope?.ServiceProvider.GetRequiredService<MainWindowViewModel>()
                ?? throw new InvalidOperationException("Chưa khởi tạo dịch vụ ứng dụng.");
            var main = new MainWindow(viewModel);
            this.MainWindow = main;
            main.Show();
            loading.Close();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        protected override void OnExit(ExitEventArgs e)
        {
            _scope?.Dispose();
            _scope = null;
            _services?.Dispose();
            _services = null;
            base.OnExit(e);
        }
        private bool HasConnectionString()
        {
            try
            {
                _ = _services?.GetRequiredService<IAppSettings>().ConnectionString;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "DeltaBat - Thiếu cấu hình kết nối",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}
