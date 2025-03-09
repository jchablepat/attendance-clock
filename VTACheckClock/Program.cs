using Avalonia;
using Avalonia.ReactiveUI;
using NLog;
using System;

namespace VTACheckClock
{
    class Program
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) {
            try {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            } catch (Exception ex) {
                log.Error(new Exception(), $"Error al cargar la App: {ex.InnerException}");
            } finally { 
            
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
