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

        var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
        MainWindow = shellWindow;
        shellWindow.Show();

        var shellViewModel = _host.Services.GetRequiredService<ShellViewModel>();

        // The database connectivity check and the initial data load run in the background,
        // without being awaited here, so that an unreachable database never delays the display of
        // the main window. The shell view model exposes the outcome through its
        // IsDatabaseConnected property, which starts in a "Disconnected" state until the check
        // completes.
        _ = shellViewModel.InitializeAsync();
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

        services.AddSingleton<IBikerRepository, PostgresBikerRepository>();
        services.AddSingleton<IRaceRepository, PostgresRaceRepository>();
        services.AddSingleton<ICategoryRepository, PostgresCategoryRepository>();
        services.AddSingleton<IRaceCategoryLinkRepository, PostgresRaceCategoryLinkRepository>();
        services.AddSingleton<IRegistrationRepository, PostgresRegistrationRepository>();
        services.AddSingleton<ICrossingRepository, PostgresCrossingRepository>();
        services.AddSingleton<IStandingRepository, PostgresStandingRepository>();
        services.AddSingleton<IDatabaseConnectionService, PostgresDatabaseConnectionService>();
        services.AddSingleton<IRaceStandingsJournalService, XmlRaceStandingsJournalService>();

        services.AddSingleton<IStandingsCalculatorService, StandingsCalculatorService>();
        services.AddSingleton<IBibNumberValidationService, BibNumberValidationService>();

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
