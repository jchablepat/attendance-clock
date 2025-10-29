using NLog;
using System;
using System.Text;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    public interface IAdminAlertService
    {
        Task NotifyErrorAsync(string title, Exception ex, object? context = null);
    }

    /// <summary>
    /// Servicio para notificar a administradores errores críticos en tiempo real vía correo.
    /// Usa la configuración SMTP en MainSettings.
    /// </summary>
    public class AdminAlertService : IAdminAlertService
    {
        private readonly Logger _log = LogManager.GetLogger("app_logger");

        public async Task NotifyErrorAsync(string title, Exception ex, object? context = null)
        {
            try
            {
                var main = RegAccess.GetMainSettings() ?? new MainSettings();
                var clock = RegAccess.GetClockSettings() ?? new ClockSettings();

                if (!main.MailEnabled)
                {
                    _log.Warn("MailEnabled está desactivado. No se enviará alerta de error.");
                    return;
                }

                string subject = $"[VTACheckClock] {title} | Oficina {clock.clock_office} | Ticket: {Guid.NewGuid()}";

                var body = BuildErrorHtml(title, ex, clock, context);

                await EmailSenderHandler.SendEmailAsync(subject, body);
            }
            catch (Exception sendEx)
            {
                _log.Error(sendEx, "Fallo al preparar alerta de error por correo.");
            }
        }

        private static string BuildErrorHtml(string title, Exception ex, ClockSettings clock, object? context)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;color:#444;'>");
            sb.Append($"<h2 style='color:#b00020;margin:0 0 8px 0;'>{title}</h2>");
            sb.Append("<p>Se ha detectado un error en el Checador de asistencia.</p>");
            sb.Append("<hr style='border:none;border-top:1px solid #eee;margin:12px 0;' />");

            sb.Append("<h3 style='margin:8px 0;'>Contexto del dispositivo</h3>");
            sb.Append("<ul style='margin:0 0 8px 18px;'>");
            sb.Append($"<li><strong>Oficina:</strong> {clock.clock_office}</li>");
            sb.Append($"<li><strong>Dispositivo:</strong> {clock.clock_uuid}</li>");
            sb.Append($"<li><strong>Fecha/Hora:</strong> {DateTime.Now:yyyy/MM/dd HH:mm:ss}</li>");
            sb.Append("</ul>");

            sb.Append("<h3 style='margin:8px 0;'>Detalle del error</h3>");
            sb.Append("<pre style='background:#f7f7f7;border:1px solid #eee;padding:10px;white-space:pre-wrap;'>");
            sb.Append(System.Net.WebUtility.HtmlEncode(ex.ToString()));
            sb.Append("</pre>");

            if (context != null)
            {
                sb.Append("<h3 style='margin:8px 0;'>Datos adicionales</h3>");
                sb.Append("<pre style='background:#f7f7f7;border:1px solid #eee;padding:10px;white-space:pre-wrap;'>");
                sb.Append(System.Net.WebUtility.HtmlEncode(context.ToString()));
                sb.Append("</pre>");
            }

            sb.Append("<p style='font-size:12px;color:#777;'>Este mensaje fue generado automáticamente por VTACheckClock.</p>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}