using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.App.ViewModels;
using NeteaseMusicCloudMatch.Infrastructure.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace NeteaseMusicCloudMatch.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Directory.CreateDirectory(AppPaths.LogDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception. ErrorType={ErrorType}", args.Exception.GetType().Name);
            MessageBox.Show("应用遇到未处理错误，请查看脱敏日志。", "NeteaseMusicCloudMatch", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(Log.Logger, dispose: true);
        builder.Services.AddNeteaseMusicCloudMatchInfrastructure(options =>
        {
            var configuredHost = builder.Configuration["NeteaseApi:BaseUri"];
            if (Uri.TryCreate(configuredHost, UriKind.Absolute, out var baseUri))
            {
                options.BaseUri = baseUri;
            }

            var configuredInterfaceHost = builder.Configuration["NeteaseApi:InterfaceBaseUri"];
            if (Uri.TryCreate(configuredInterfaceHost, UriKind.Absolute, out var interfaceBaseUri))
            {
                options.InterfaceBaseUri = interfaceBaseUri;
            }

            if (int.TryParse(builder.Configuration["NeteaseApi:TimeoutSeconds"], out var seconds) && seconds is >= 5 and <= 120)
            {
                options.Timeout = TimeSpan.FromSeconds(seconds);
            }
        });
        builder.Services.AddSingleton<Wpf.Ui.IContentDialogService, Wpf.Ui.ContentDialogService>();
        builder.Services.AddSingleton<Wpf.Ui.ISnackbarService, Wpf.Ui.SnackbarService>();
        builder.Services.AddSingleton<IUserDialogService, UserDialogService>();
        builder.Services.AddSingleton<IExternalNavigationService, ExternalNavigationService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IApplicationLogReader, ApplicationLogReader>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<MatchViewModel>();
        builder.Services.AddSingleton<CloudDriveViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<LogViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
        await window.ViewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
