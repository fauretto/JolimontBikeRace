using System.IO;
using System.Windows;
using System.Windows.Threading;
using JolimontBikeRace.App.Services;
using JolimontBikeRace.App.ViewModels;
using JolimontBikeRace.App.Views;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Services;
using JolimontBikeRace.Data;
using JolimontBikeRace.Data.Interfaces;
using JolimontBikeRace.Data.Repositories;
using JolimontBikeRace.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JolimontBikeRace.App;

/// <summary>
/// Provides the composition root of the application: it builds the dependency injection host,
/// registers every repository, service and view model, shows the main window, and wires the
/// global exception handlers that keep the application alive while logging any unexpected
/// failure.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Builds the dependency injection host, configures logging, shows the main window and starts
    /// the initial, non-blocking database connectivity check.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log4NetLogService.Configure();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) => ConfigureServices(context.Configuration, services))
            .Build();

        var logService = _host.Services.GetRequiredService<ILogService>();

        // This log statement, together with the matching one in OnExit, illustrates the
        // "ClassName -> MethodName : message" logging convention that every part of the
        // application must follow.
        logService.Information("App -> OnStartup", "application starting");

        // Keep the application alive while only the splash window is visible, so that closing the
        // splash before the main window is shown does not shut the application down. The normal
        // shutdown mode is restored once the main window has been shown.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Show the "Checking the database, please wait" splash, verify (and if necessary create)
        // the database, and only then show the main window. This runs without being awaited here
        // so that the user interface message loop stays responsive and the splash animates.
        _ = StartApplicationAsync();
    }

    // Shows the splash window, ensures the database exists, and then shows the main window. Runs
    // as a fire-and-forget task started from OnStartup so the user interface stays responsive.
    private async Task StartApplicationAsync()
    {
        var logService = _host!.Services.GetRequiredService<ILogService>();
        var brandingProvider = _host.Services.GetRequiredService<IBrandingProvider>();

        var splashWindow = new StartupSplashWindow(brandingProvider.RaceName);
        splashWindow.Show();

        try
        {
            var initializationService = _host.Services.GetRequiredService<IDatabaseInitializationService>();
            var outcome = await EnsureDatabaseWithRetryAsync(initializationService, logService);

            if (outcome is null)
            {
                // The user chose to close the application from the retry dialog; shutdown is in
                // progress, so simply close the splash and stop.
                splashWindow.Close();
                return;
            }

            var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
            MainWindow = shellWindow;
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            shellWindow.Show();
            splashWindow.Close();

            if (outcome == DatabaseInitializationOutcome.Created)
            {
                logService.Information("App -> StartApplicationAsync", "a new database was created at startup");
                MessageBox.Show(
                    "A new, empty database was created for this application.",
                    "Database Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            var shellViewModel = _host.Services.GetRequiredService<ShellViewModel>();

            // The database connectivity check and the initial data load run in the background,
            // without being awaited here, so that the main window is shown without delay.
            _ = shellViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            // EnsureDatabaseWithRetryAsync already handles database errors, so reaching here means
            // an unexpected failure. Close the splash, report it, and shut down rather than leaving
            // the application stuck on the splash window.
            logService.Error("App -> StartApplicationAsync", "an unexpected failure occurred during startup", exception);
            splashWindow.Close();
            MessageBox.Show(exception.Message, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    // Runs the database existence check and creation, showing a Retry/Cancel dialog when it fails.
    // Returns the outcome on success, or null when the user cancels, in which case the application
    // is shut down.
    private async Task<DatabaseInitializationOutcome?> EnsureDatabaseWithRetryAsync(IDatabaseInitializationService initializationService, ILogService logService)
    {
        while (true)
        {
            try
            {
                return await initializationService.EnsureDatabaseExistsAsync();
            }
            catch (Exception exception)
            {
                logService.Error("App -> EnsureDatabaseWithRetryAsync", "failed to verify or create the database", exception);
                var choice = MessageBox.Show(
                    $"The application could not verify or create its database.\n\n{exception.Message}\n\nClick Retry to try again, or Cancel to close the application.",
                    "Checking The Database",
                    MessageBoxButton.RetryCancel,
                    MessageBoxImage.Error);
                if (choice != MessageBoxResult.Retry)
                {
                    Shutdown();
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Logs the application shutdown and releases the dependency injection host.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var logService = _host.Services.GetRequiredService<ILogService>();

            // This log statement, together with the matching one in OnStartup, illustrates the
            // "ClassName -> MethodName : message" logging convention that every part of the
            // application must follow.
            logService.Information("App -> OnExit", "application closing");

            _host.Dispose();
        }

        base.OnExit(e);
    }

    // Registers every repository, service and view model of the application with the dependency
    // injection container. Repositories and services are singletons because they are stateless
    // (or hold only shared configuration), and view models are singletons so that the shell can
    // navigate between them without losing their state.
    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<ILogService, Log4NetLogService>();

        var connectionString = configuration["Database:ConnectionString"] ?? string.Empty;
        services.AddSingleton<IConnectionStringProvider>(new ConnectionStringProvider(connectionString));

        var brandingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "branding.xml");
        services.AddSingleton<IBrandingProvider>(serviceProvider =>
            new XmlBrandingProvider(brandingFilePath, serviceProvider.GetRequiredService<ILogService>()));

        services.AddSingleton<IBikerRepository, PostgresBikerRepository>();
        services.AddSingleton<IRaceRepository, PostgresRaceRepository>();
        services.AddSingleton<ICategoryRepository, PostgresCategoryRepository>();
        services.AddSingleton<IRaceCategoryLinkRepository, PostgresRaceCategoryLinkRepository>();
        services.AddSingleton<IRegistrationRepository, PostgresRegistrationRepository>();
        services.AddSingleton<ICrossingRepository, PostgresCrossingRepository>();
        services.AddSingleton<IStandingRepository, PostgresStandingRepository>();
        services.AddSingleton<IDatabaseConnectionService, PostgresDatabaseConnectionService>();
        services.AddSingleton<IDatabaseInitializationService, PostgresDatabaseInitializationService>();
        services.AddSingleton<IRaceStandingsJournalService, XmlRaceStandingsJournalService>();

        services.AddSingleton<IStandingsCalculatorService, StandingsCalculatorService>();
        services.AddSingleton<IBibNumberValidationService, BibNumberValidationService>();
        services.AddSingleton<IRaceCollectionService, RaceCollectionService>();

        services.AddSingleton<RaceManagerViewModel>();
        services.AddSingleton<BikersViewModel>();
        services.AddSingleton<ChronoViewModel>();
        services.AddSingleton<StandingsViewModel>();
        services.AddSingleton<ShellViewModel>();

        services.AddSingleton<ShellWindow>();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new InvalidOperationException("An unknown, non-exception error object was raised.");
        _host?.Services.GetService<ILogService>()?.Error("App -> OnUnhandledException", "an unhandled exception was raised in a background thread", exception);
        MessageBox.Show(exception.Message, "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host?.Services.GetService<ILogService>()?.Error("App -> OnDispatcherUnhandledException", "an unhandled exception was raised on the user interface thread", e.Exception);
        MessageBox.Show(e.Exception.Message, "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
