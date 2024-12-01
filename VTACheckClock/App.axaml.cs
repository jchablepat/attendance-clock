using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NLog;
using System;
using System.Globalization;
using VTACheckClock.Services.Libs;
using VTACheckClock.ViewModels;
using VTACheckClock.Views;

namespace VTACheckClock
{
    public partial class App : Application
    {
        private readonly Logger log = LogManager.GetLogger("app_logger");

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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Display first dialog to VALIDATE main Window
                var dialog = new ConfigurationWindow() {
                    DataContext = new ConfigurationViewModel(),
                };

                // and subscribe to its "Apply" button, which returns the dialog result
                dialog.ViewModel!.ApplyCommand
                .Subscribe(result => {
                   var mw = new MainWindow {
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

                desktop.Exit += (sender, args) => {
                    log.Warn("La aplicación ha finalizado.");
                    if(!GlobalVars.IsRestart) {
                        // Asegúrate de que la aplicación se cierre completamente
                        Environment.Exit(0);
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
