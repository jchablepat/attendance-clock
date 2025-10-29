using NLog;
using System;
using System.Data;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    class EmailSenderHandler
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        private static bool SetMailConfig(SmtpClient oSmtpClient, ref string recipients)
        {
            bool setConfig = false;
            var config = RegAccess.GetMainSettings() ?? new MainSettings();

            var host = config.MailServer; // tuServidorSmtp
            bool validPort = int.TryParse(config?.MailPort, out int port);
            var username = config.MailUser;
            var password = config.MailPass;
            bool IsEnabled = config.MailEnabled;
            recipients = config.MailRecipient ?? "";

            bool hasHost = !string.IsNullOrWhiteSpace(host);
            bool hasPort = validPort && port != 0;
            bool hasUser = !string.IsNullOrWhiteSpace(username);
            bool hasPass = !string.IsNullOrWhiteSpace(password);
            bool hasRecipients = !string.IsNullOrEmpty(recipients);

            if (hasHost && hasPort && hasUser && hasPass && hasRecipients && IsEnabled)
            {
                oSmtpClient.Host = host!;
                oSmtpClient.Port = port;
                oSmtpClient.Credentials = new NetworkCredential(username, password);
                oSmtpClient.UseDefaultCredentials = false;
                oSmtpClient.EnableSsl = true;
                oSmtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                setConfig = true;
            }

            if (!setConfig && IsEnabled)
            {
                // Log detallado de configuración faltante o inválida
                var detalles = $"Host='{host}', Port='{config?.MailPort}', User='{username}', Recipients='{recipients}', Habilitado={IsEnabled}";
                var faltantes = new StringBuilder();
                if (!IsEnabled) faltantes.Append("MailEnabled=false; ");
                if (!hasHost) faltantes.Append("Host vacío; ");
                if (!hasPort) faltantes.Append("Puerto inválido; ");
                if (!hasUser) faltantes.Append("Usuario vacío; ");
                if (!hasPass) faltantes.Append("Contraseña vacía; ");
                if (!hasRecipients) faltantes.Append("Destinatarios vacíos; ");

                log.Warn($"Configuración SMTP incompleta/incorrecta. {detalles}. Falta: {faltantes.ToString().Trim()}");
            }

            return setConfig;
        }

        public static async Task SendEmailAsync(string subject, string body)
        {
            try {
                using SmtpClient oSmtpClient = new();
                string recipients = "";

                if (!SetMailConfig(oSmtpClient, ref recipients)) {
                    return;
                }

                oSmtpClient.Timeout = 30000; // 30s

                NetworkCredential? credentials = oSmtpClient?.Credentials as NetworkCredential;

                var oMailMessage = new MailMessage {
                    From = new MailAddress(credentials?.UserName ?? "", "VTSoftware")
                };

                SetToAddress(ref oMailMessage, recipients);

                oMailMessage.Subject = subject;
                oMailMessage.SubjectEncoding = Encoding.UTF8;
                oMailMessage.Body = body;
                oMailMessage.BodyEncoding = Encoding.UTF8;
                oMailMessage.IsBodyHtml = true;

                await oSmtpClient.SendMailAsync(oMailMessage);
                //log.Info($"Correo enviado correctamente a: {recipients}");
            }
            catch (SmtpFailedRecipientsException ex)
            {
                foreach (var inner in ex.InnerExceptions)
                {
                    log.Warn(inner, $"Fallo SMTP para destinatario '{inner.FailedRecipient}' (Status: {inner.StatusCode})");
                }
                log.Warn(ex, "Error SMTP: múltiples destinatarios fallidos");
            }
            catch (SmtpFailedRecipientException ex)
            {
                log.Warn(ex, $"Fallo SMTP para destinatario '{ex.FailedRecipient}' (Status: {ex.StatusCode})");
            }
            catch (SmtpException smtpEx) {
                log.Warn(smtpEx, $"Error SMTP al enviar el correo (Status: {smtpEx.StatusCode}). Detalles: {smtpEx.ToString()}");
            }
            catch (Exception ex)
            {
                log.Warn(ex, $"Error general al enviar el correo.");
            }
        }

        /// <summary>
        /// Add all the recipients to whom information will be sent
        /// </summary>
        /// <param name="oMailMessage"></param>
        /// <param name="emails">Mails concatenated in a text string with a special character.</param>
        private static void SetToAddress(ref MailMessage oMailMessage, string emails)
        {
            char[] separators = [',', ';'];
            //int added = 0;
            foreach (var email in SplitEmailsByDelimiter(emails, separators))
            {
                var em = email.Trim();
                try
                {
                    oMailMessage.To.Add(em);
                    //added++;
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Dirección de correo inválida ignorada: '{em}'");
                }
            }
            //log.Debug($"Destinatarios agregados: {added}");
        }

        /// <summary>
        /// Useful to separate multiple concatenated emails.
        /// </summary>
        /// <param name="emails"></param>
        /// <param name="separators"></param>
        /// <returns></returns>
        public static string[] SplitEmailsByDelimiter(string emails, char[] separators)
        {
            return emails.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string BuildMessage(DataTable dt, DataTable dupPunches)
        {
            var emp_list = ExportDatatableToHtml(dt);
            var duplicatedPunches = ExportDatatableToHtml(dupPunches);
            var duplicatedHtml = string.IsNullOrEmpty(duplicatedPunches) ? "": $"""
                <p>Checadas Duplicadas</p>
                {duplicatedPunches}
            """;

            var body_msg = "<div style='box-sizing:border-box;background-color:#ffffff;color:#718096;height:100%;line-height:1.4;margin:0;padding:0;width:100%!important'>";
            body_msg +=
                $"""
                    <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="box-sizing:border-box;background-color:#edf2f7;margin:0;padding:0;width:100%;">
                    <tbody><tr>
                        <td align="center" style="box-sizing:border-box;">
                            <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="box-sizing:border-box;margin:0;padding:0;width:100%">
                                <tbody>
                                    <tr>
                                        <td width="100%" cellpadding="0" cellspacing="0" style="box-sizing:border-box;background-color:#edf2f7; border-bottom:1px solid #edf2f7;border-top:1px solid #edf2f7;margin:0;padding:0;width:100%;">
                                            <table align="center" width="570" cellpadding="0" cellspacing="0" role="presentation" style="box-sizing:border-box;background-color:#ffffff;border-color:#e8e5ef;border-radius:2px;border-width:1px;margin:0 auto;padding:0;width:570px">
                                                <tbody>
                                                    <tr>
                                                        <td style="box-sizing:border-box;max-width:100vw;padding:32px">
                                                            <h1 style="box-sizing:border-box;color:#3d4852;font-size:18px;font-weight:bold;margin-top:0;text-align:left">Estimado administrador,</h1>
                                                            <p style='box-sizing:border-box;font-size:16px;line-height:1.5em;margin-top:0;text-align:left'>
                                                                "Le escribo para informarle que algunos empleados no tienen checadas de entradas o salidas en la fecha actual. 
                                                                "Esto puede indicar que no han asistido al trabajo o que han tenido algún problema con el sistema de registro. 
                                                                "Le pido que revise la situación y tome las medidas necesarias.
                                                            </p> 
                                                            {emp_list}
                                                            {duplicatedHtml}
                                                           <p style='box-sizing:border-box;font-size:16px;line-height:1.5em;text-align:left'>Atentamente,</p>
                                                           <p style='box-sizing:border-box;font-size:16px;line-height:1.5em;margin-top:0;text-align:left'>El sistema de control de asistencia</p>
                                                        </td>
                                                    </tr>
                                                </tbody>
                                            </table>
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </td>
                    </tr>
                    </tbody>
                </table></div>
               """;

            return body_msg;
        }

        /// <summary>
        /// Funcion genérica para convertir un DataTable a una tabla HTML.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static string ExportDatatableToHtml(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
            {
                return "";
            }

            StringBuilder strHTMLBuilder = new();
            strHTMLBuilder.Append("<table border='1' cellpadding='0' cellspacing='0' style='border:0;border-style:hidden;'>");
            strHTMLBuilder.Append("<thead>");
            strHTMLBuilder.Append("<tr>");
            foreach (DataColumn myColumn in dt.Columns)
            {
                strHTMLBuilder.Append("<th>");
                strHTMLBuilder.Append(myColumn.ColumnName);
                strHTMLBuilder.Append("</th>");
            }
            strHTMLBuilder.Append("</tr>");
            strHTMLBuilder.Append("</thead>");
            strHTMLBuilder.Append("<tbody>");

            foreach (DataRow myRow in dt.Rows)
            {
                strHTMLBuilder.Append("<tr>");
                foreach (DataColumn myColumn in dt.Columns)
                {
                    strHTMLBuilder.Append("<td style='padding: 3px;'>");
                    strHTMLBuilder.Append(myRow[myColumn.ColumnName].ToString());
                    strHTMLBuilder.Append("</td>");
                }
                strHTMLBuilder.Append("</tr>");
            }
            strHTMLBuilder.Append("</tbody>");
            strHTMLBuilder.Append("</table>");

            string Htmltext = strHTMLBuilder.ToString();

            return Htmltext;
        }
    }
}
