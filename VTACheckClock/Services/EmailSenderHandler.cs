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

        private static bool SetMailConfig(ref SmtpClient oSmtpClient, ref string recipients)
        {
            bool setConfig = false;
            var config = RegAccess.GetMainSettings() ?? new MainSettings();

            var host = config.MailServer; // tuServidorSmtp
            int port = !string.IsNullOrEmpty(config?.MailPort) ? int.Parse(config.MailPort) : 0;
            var username = config.MailUser;
            var password = config.MailPass;
            bool IsEnabled = config.MailEnabled;
            recipients = config.MailRecipient ?? "";
            
            if (!string.IsNullOrWhiteSpace(host) && port != 0 && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(recipients) && !string.IsNullOrWhiteSpace(password) && IsEnabled)
            {
                oSmtpClient.Host = host;
                oSmtpClient.Port = port;
                oSmtpClient.Credentials = new NetworkCredential(username, password);
                oSmtpClient.EnableSsl = true;
                oSmtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                setConfig = true;
            }

            return setConfig;
        }

        public static async Task SendEmailAsync(string subject, string body)
        {
            try {
                SmtpClient oSmtpClient = new();
                string recipients = "";

                if (!SetMailConfig(ref oSmtpClient, ref recipients)) {
                    return;
                }

                NetworkCredential? credentials = oSmtpClient?.Credentials as NetworkCredential;

                var oMailMessage = new MailMessage {
                    From = new MailAddress(credentials?.UserName ?? "", "VTSoftware")
                };

                SetToAddress(ref oMailMessage, recipients);

                oMailMessage.Subject = subject;
                oMailMessage.Body = body;
                oMailMessage.IsBodyHtml = true;

                await oSmtpClient.SendMailAsync(oMailMessage);
            }
            catch (Exception ex) {
                log.Warn($"Error al enviar el correo: {ex.Message} => { ex.InnerException?.Message ?? "" }");
            }
        }

        /// <summary>
        /// Add all the recipients to whom information will be sent
        /// </summary>
        /// <param name="oMailMessage"></param>
        /// <param name="emails">Mails concatenated in a text string with a special character.</param>
        private static void SetToAddress(ref MailMessage oMailMessage, string emails)
        {
            char[] separators = { ',', ';' };
            foreach (var email in SplitEmailsByDelimiter(emails, separators))
            {
                oMailMessage.To.Add(email.Trim());
            }
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

        public static string BuildMessage(DataTable dt)
        {
            var emp_list = ExportDatatableToHtml(dt);
            var body_msg = "<div style='box-sizing:border-box;background-color:#ffffff;color:#718096;height:100%;line-height:1.4;margin:0;padding:0;width:100%!important'>";
            body_msg += 
                "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"box-sizing:border-box;background-color:#edf2f7;margin:0;padding:0;width:100%;\">" +
                    "<tbody><tr>" +
                        "<td align=\"center\" style=\"box-sizing:border-box;\">" +
                            "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"box-sizing:border-box;margin:0;padding:0;width:100%\">" +
                                "<tbody>" +
                                    "<tr>" +
                                        "<td width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"box-sizing:border-box;background-color:#edf2f7; border-bottom:1px solid #edf2f7;border-top:1px solid #edf2f7;margin:0;padding:0;width:100%;\">" +
                                            "<table align=\"center\" width=\"570\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"box-sizing:border-box;background-color:#ffffff;border-color:#e8e5ef;border-radius:2px;border-width:1px;margin:0 auto;padding:0;width:570px\">" +
                                                "<tbody>" +
                                                    "<tr>" +
                                                        "<td style=\"box-sizing:border-box;max-width:100vw;padding:32px\">" +
                                                            "<h1 style=\"box-sizing:border-box;color:#3d4852;font-size:18px;font-weight:bold;margin-top:0;text-align:left\">Estimado administrador,</h1>" +
                                                            "<p style='box-sizing:border-box;font-size:16px;line-height:1.5em;margin-top:0;text-align:left'>" +
                                                                "Le escribo para informarle que algunos empleados no tienen checadas de entradas o salidas en la fecha actual. " +
                                                                "Esto puede indicar que no han asistido al trabajo o que han tenido algún problema con el sistema de registro. " +
                                                                "Le pido que revise la situación y tome las medidas necesarias." +
                                                            "</p>" + emp_list +
                                                           "<p style='box-sizing:border-box;font-size:16px;line-height:1.5em;text-align:left'>Atentamente,</p>" +
                                                           "<p style='box-sizing:border-box;font-size:16px;line-height:1.5em;margin-top:0;text-align:left'>El sistema de control de asistencia</p>" +
                                                        "</td>" +
                                                    "</tr>" +
                                                "</tbody>" +
                                            "</table>" +
                                        "</td>" +
                                    "</tr>" +
                                "</tbody>" +
                            "</table>" +
                        "</td>" +
                    "</tr>" +
                    "</tbody>" +
                "</table></div>";

            return body_msg;
        }

        public static string ExportDatatableToHtml(DataTable dt)
        {
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
