using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using DIG.App.Services;
using DIG.App.ViewModels;
using DIG.App.Views;
using DIG.Core.Ai;
using DIG.Core.Data;
using DIG.Core.Intelligence;
using DIG.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DIG.App;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDir, "dig-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            _services = BuildServiceProvider();
            InitialiseDatabase(_services);

            var settings = _services.GetRequiredService<AppSettingsService>();
            ThemeManager.Apply(settings.Theme);

            var window = _services.GetRequiredService<MainWindow>();
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup");
            MessageBox.Show($"D!G! failed to start:\n\n{ex.Message}", "D!G!",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(dispose: true);
        });

        // Data
        services.AddSingleton(new SqliteConnectionFactory(AppPaths.DatabasePath));
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<BookmarkRepository>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<EmailTemplateRepository>();
        services.AddSingleton<BuildCacheRepository>();
        services.AddSingleton<InfrastructureRepository>();
        services.AddSingleton<WorkspaceRepository>();

        // Security
        services.AddSingleton<ISecretStore>(_ => new DpapiSecretStore());

        // Settings
        services.AddSingleton<AppSettingsService>();

        // AI
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
        services.AddSingleton<IAiProvider, GeminiAiProvider>();
        services.AddSingleton<AiService>();

        // VMware build lookup (source-adapter architecture). Order matters: the
        // authoritative browser-imported source is consulted first (Verified),
        // then CSV imports and the bundled index (Possible).
        services.AddSingleton<IBuildSource>(sp => new CachedBuildSource(
            sp.GetRequiredService<BuildCacheRepository>(), "browser-import",
            "Imported from a Broadcom page (browser)", authoritative: true));
        services.AddSingleton<IBuildSource>(sp => new CachedBuildSource(
            sp.GetRequiredService<BuildCacheRepository>(), "csv",
            "Imported from CSV", authoritative: false));
        services.AddSingleton<IBuildSource, LocalIndexBuildSource>();
        services.AddSingleton<VmwareBuildLookupService>();

        // View-models
        services.AddSingleton<MainViewModel>();
        services.AddTransient<AiAssistantViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<EmailTemplatesViewModel>();
        services.AddTransient<BuildLookupViewModel>();
        services.AddTransient<BookmarksViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<InfrastructureViewModel>();
        services.AddTransient<WorkspacesViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static void InitialiseDatabase(IServiceProvider sp)
    {
        var factory = sp.GetRequiredService<SqliteConnectionFactory>();
        using (var c = factory.Open())
        {
            DatabaseMigrator.Migrate(c);
        }
        sp.GetRequiredService<EmailTemplateRepository>().SeedBuiltInsIfEmpty();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}", "D!G!",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
