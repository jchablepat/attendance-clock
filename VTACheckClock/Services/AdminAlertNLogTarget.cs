using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Targets;
using System;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services
{
    /// <summary>
    /// Represents a custom NLog target that sends administrative alerts for log events with a severity level of fatal
    /// or higher.
    /// </summary>
    /// <remarks>This target is designed to send alerts to administrators when critical log events occur. It
    /// checks for internet connectivity and uses a background queue for resilience. If the queue is unavailable, it
    /// falls back to sending alerts directly.</remarks>
    [Target("AdminAlertTarget")]
    public sealed class AdminAlertNLogTarget : TargetWithLayout
    {
        protected override void Write(LogEventInfo logEvent)
        {
            try
            {
                // Validar si hay Internet antes de intentar enviar alertas
                if (logEvent.Level < LogLevel.Fatal || GlobalVars.BeOffline)
                    return;

                //var main = RegAccess.GetMainSettings() ?? new MainSettings();
                var clock = RegAccess.GetClockSettings() ?? new ClockSettings();

                var alert = new AdminErrorAlert
                {
                    Title = $"{logEvent.Level} en aplicación",
                    Severity = logEvent.Level.Name,
                    Message = logEvent.FormattedMessage,
                    Exception = logEvent.Exception?.ToString(),
                    Context = logEvent.Properties?.Count > 0 ? string.Join("; ", logEvent.Properties) : null,
                    OfficeId = clock.clock_office.ToString(),
                    DeviceUUID = clock.clock_uuid ?? string.Empty,
                    Timestamp = DateTime.Now
                };

                // Usar cola de segundo plano para resiliencia
                try
                {
                    var queue = App.ServiceProvider.GetRequiredService<AdminAlertBackgroundQueue>();
                    queue.Enqueue(alert);
                }
                catch
                {
                    // fallback directo si la cola no está disponible
                    Task.Run(async () =>
                    {
                        //try
                        //{
                        //    var realtime = App.ServiceProvider.GetRequiredService<IRealtimeService>();
                        //    await realtime.SendAdminAlertAsync(alert);
                        //}
                        //catch { }

                        try
                        {
                            var adminService = App.ServiceProvider.GetRequiredService<IAdminAlertService>();
                            var alertException = new Exception(alert.Exception ?? alert.Message ?? "Error");
                            await adminService.NotifyErrorAsync(alert.Title ?? "Error", alertException, alert);
                        }
                        catch { }
                    });
                }
            }
            catch
            {
                // never throw from target, just ignore.
            }
        }
    }
}