using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using ReactiveUI;
using System;
using System.Net.Http;
using System.Reactive;
using VTACheckClock.Services;
using VTACheckClock.Services.Audit;
using VTACheckClock.Services.Auth;
using VTACheckClock.Services.Libs;
using VTACheckClock.ViewModels;
using VTACheckClock.Views;

namespace VTACheckClock
{
    public partial class App : Application
    {
        private readonly Logger log = LogManager.GetLogger("app_logger");
        private static ServiceProvider? serviceProvider;

        public static ServiceProvider ServiceProvider
        {
            get => serviceProvider!;
            private set => serviceProvider = value;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? exception = e.ExceptionObject as Exception;
            // Registra o muestra la excepción en el registro de eventos, archivo de registro, etc.
            log.Error(exception, "Excepción no controlada: ");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Manejar errores de ReactiveUI
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
            {
                log.Error(ex, "ReactiveUI Error: ");
            });

            var services = new ServiceCollection();
            // Registrar servicios como Singleton
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<SignalRClient>();
            services.AddSingleton<IRealtimeService, RealtimeService>();
            services.AddSingleton<TimeChangeAuditService>();
            services.AddSingleton<UIService>();
            services.AddSingleton<ClockService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IAdminAlertService, AdminAlertService>();
            services.AddSingleton<AdminAlertBackgroundQueue>();

            ServiceProvider = services.BuildServiceProvider();

            // Inicializar el servicio de logging (NLog + Seq)
            var logging = ServiceProvider.GetRequiredService<ILoggingService>();
            logging.Initialize();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Display first dialog to VALIDATE main Window
                var dialog = new ConfigurationWindow()
                {
                    DataContext = new ConfigurationViewModel(),
                };

                // and subscribe to its "Apply" button, which returns the dialog result
                dialog.ViewModel!.ApplyCommand
                .Subscribe(result =>
                {
                    var mw = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(result),
                    };

                    desktop.MainWindow = mw;
                    mw.Show();
                    dialog.Close();
                });

                desktop.MainWindow = dialog;
                //desktop.MainWindow = new MainWindow
                //{
                //    DataContext = new MainWindowViewModel(""),
                //};

                desktop.Exit += (sender, args) =>
                {
                    log.Warn("La aplicación ha finalizado.");
                    LogManager.Shutdown();

                    if (!GlobalVars.IsRestart)
                    {
                        // Asegúrate de que la aplicación se cierre completamente
                        Environment.Exit(0);
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
