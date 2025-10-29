using Microsoft.Win32;
using NLog;
using System;
using System.Reactive;
using System.Runtime.InteropServices;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services
{
    class RegAccess
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        /// <summary>
        /// Obtiene las configuraciones generales almacenadas en el Registro de Windows en un objeto MainSettings.
        /// </summary>
        /// <returns>Objeto MainSettings con la información de configuración.</returns>
        public static MainSettings? GetMainSettings()
        {
            try {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    RegistryKey? la_key = The_key();

                   if (la_key != null) {
                        MainSettings la_resp = new() {
                            Ws_url = GetRegValue("ws_url"),
                            Db_server = GetRegValue("db_server"),
                            Db_name = GetRegValue("db_name"),
                            Db_user = GetRegValue("db_user"),
                            Db_pass = GetRegValue("db_pass"),
                            Ftp_url = GetRegValue("ftp_url"),
                            Ftp_port = GetRegValue("ftp_port"),
                            Ftp_user = GetRegValue("ftp_user"),
                            Ftp_pass = GetRegValue("ftp_pass"),
                            Path_tmp = GetRegValue("path_tmp"),
                            Logo = GetRegValue("logo"),
                            //These parameters are optionals so can be empty.
                            Employees_host = GetRegValue("employees_host"),
                            Websocket_enabled = GetBoolFromByte("websocket_enabled"),
                            Websocket_host = GetRegValue("websocket_host"),
                            Websocket_port = GetRegValue("websocket_port"),
                            Pusher_app_id = GetRegValue("pusher_app_id"),
                            Pusher_key = GetRegValue("pusher_key"),
                            Pusher_secret = GetRegValue("pusher_secret"),
                            Pusher_cluster = GetRegValue("pusher_cluster"),
                            Event_name = GetRegValue("event_name"),
                            MailEnabled = GetBoolFromByte("mailEnabled"),
                            MailServer = GetRegValue("mailServer"),
                            MailPort = GetRegValue("mailPort"),
                            MailUser = GetRegValue("mailUser"),
                            MailPass = GetRegValue("mailPass"),
                            MailRecipient = GetRegValue("mailRecipient"),
                            UsePusher = GetBoolFromByte("usePusher"),
                            SignalRHubUrl = GetRegValue("signalRHubUrl"),
                            SignalRHubName = GetRegValue("signalRHubName"),
                            SignalRMethodName = GetRegValue("signalRMethodName"),
                            SignalRAdminMethodName = GetRegValue("signalRAdminMethodName"),
                            SignalRApiKey = GetRegValue("signalRApiKey"),
                            SeqUrl = GetRegValue("seqUrl"),
                            SeqApiKey = GetRegValue("seqApiKey")
                        };

                        la_key.Close();

                        return la_resp;
                   }
                   return null;
                }
                return null;
            } catch {
                return null;
            }
        }

        public static byte[]? SetNullValue(object? objVal)
        {
            return string.IsNullOrEmpty(objVal?.ToString()) ? null : (byte[])objVal;
        }

        public static bool GetBoolFromByte(string key)
        {
            RegistryKey? la_key = The_key();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && la_key != null)
            {
                SimpleAES? aes_crypt = new();
                byte[]? byteValue = SetNullValue(la_key.GetValue(key));

                return (byteValue != null) && Convert.ToBoolean(aes_crypt.DecryptFromBytes(byteValue));
            }

            return false;
        }

        public static int GetIntFromByte(string key)
        {
            RegistryKey? la_key = The_key();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && la_key != null)
            {
                SimpleAES? aes_crypt = new();
                byte[]? byteValue = SetNullValue(la_key.GetValue(key));

                return (byteValue != null) ? Convert.ToInt32(aes_crypt.DecryptFromBytes(byteValue)) : 0;
            }

            return 0;
        }

        /// <summary>
        /// Abre la clave del registro con la que trabajará el sistema (Editor del Registro de Windows).
        /// </summary>
        /// <returns>La clave del registro del sistema.</returns>
        public static RegistryKey? The_key()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                RegistryKey? la_resp = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
                la_resp = la_resp.OpenSubKey(@"SOFTWARE", true);
                la_resp = la_resp.CreateSubKey(GlobalVars.DefRegKey);

                return la_resp;
            }
            return null;
        }

        /// <summary>
        /// Obtiene las configuraciones del Reloj almacenadas en el Registro de Windows en un objeto ClockSettings.
        /// </summary>
        /// <returns>Objeto ClockSettings con la información de configuración.</returns>
        public static ClockSettings? GetClockSettings()
        {
            try {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    RegistryKey? la_key = The_key();

                    if (la_key != null) {
                        SimpleAES? aes_crypt = new();

                        ClockSettings la_resp = new() {
                            clock_office = GetIntFromByte("clock_office"),
                            clock_user = GetRegValue("clock_user", null),
                            clock_pass = GetRegValue("clock_pass", null),
                            clock_uuid = GetRegValue("clock_uuid", null),
                            clock_timezone = GetRegValue("clock_timezone")
                        };

                        la_key.Close();
                        aes_crypt = null;

                        return la_resp;
                    }
                    return null;
                }
                return null;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Recupera las configuraciones de la aplicación desde el Registro de Windows.
        /// </summary>
        /// <param name="msetts">Objeto de configuraciones generales que se proporciona como variable de salida.</param>
        /// <param name="csetts">Objeto de configuraciones del reloj que se proporciona como variable de salida.</param>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        public static bool GetRegSettings(out MainSettings? msetts, out ClockSettings? csetts)
        {
            try {
                msetts = null;
                csetts = null;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    RegistryKey? la_key = The_key();

                    if (la_key != null) {
                        msetts = new MainSettings {
                            Ws_url = GetRegValue("ws_url"),
                            Ftp_url = GetRegValue("ftp_url"),
                            Ftp_port = GetRegValue("ftp_port"),
                            Ftp_user = GetRegValue("ftp_user"),
                            Ftp_pass = GetRegValue("ftp_pass"),
                            Path_tmp = GetRegValue("path_tmp"),
                            Logo = GetRegValue("logo"),
                            //These parameters are optionals so can be empty.
                            Employees_host = GetRegValue("employees_host"),
                            Websocket_enabled = GetBoolFromByte("websocket_enabled"),
                            Websocket_host = GetRegValue("websocket_host"),
                            Websocket_port = GetRegValue("websocket_port"),
                            Pusher_app_id = GetRegValue("pusher_app_id"),
                            Pusher_key = GetRegValue("pusher_key"),
                            Pusher_secret = GetRegValue("pusher_secret"),
                            Pusher_cluster = GetRegValue("pusher_cluster"),
                            Event_name = GetRegValue("event_name"),
                            MailEnabled = GetBoolFromByte("mailEnabled"),
                            MailServer = GetRegValue("mailServer"),
                            MailPort = GetRegValue("mailPort"),
                            MailUser = GetRegValue("mailUser"),
                            MailPass = GetRegValue("mailPass"),
                            MailRecipient = GetRegValue("mailRecipient"),
                            UsePusher = GetBoolFromByte("usePusher"),
                            SignalRHubUrl = GetRegValue("signalRHubUrl"),
                            SignalRHubName = GetRegValue("signalRHubName"),
                            SignalRMethodName = GetRegValue("signalRMethodName"),
                            SignalRAdminMethodName = GetRegValue("signalRAdminMethodName"),
                            SignalRApiKey = GetRegValue("signalRApiKey"),
                            SeqUrl = GetRegValue("seqUrl"),
                            SeqApiKey = GetRegValue("seqApiKey")
                        };

                        if (GlobalVars.VTAttModule == 1) {
                            csetts = new ClockSettings {
                                clock_office = GetIntFromByte("clock_office"),
                                clock_user = GetRegValue("clock_user", null),
                                clock_pass = GetRegValue("clock_pass", null),
                                clock_uuid = GetRegValue("clock_uuid", null),
                                clock_timezone = GetRegValue("clock_timezone")
                            };

                            GlobalVars.TimeZone = csetts.clock_timezone;
                        } else {
                            csetts = null;
                        }

                        la_key.Close();

                        return true;
                    }
                    return false;
                }
                return false;
            } catch {
                msetts = null;
                csetts = null;
                return false;
            }
        }

        /// <summary>
        /// Obtiene la información de la oficina de trabajo del Registro de Windows.
        /// </summary>
        /// <returns>Objeto con la información de la oficina configurada.</returns>
        public static OfficeData GetOffRegData()
        {
            _ = int.TryParse(GetRegValue("office_id"), out int off_id);

            return new OfficeData {
                Offid = off_id,
                Offname = GetRegValue("office_name"),
                Offdesc = GetRegValue("office_desc")
            };
        }

        /// <summary>
        /// Método universal para recuperar valores del Registro de Windows.
        /// </summary>
        /// <param name="key">Nombre del valor que se recuperará.</param>
        /// <param name="dValue">Valor por defecto.</param>
        /// <returns>Cadena de texto con el valor solicitado, o null en caso de fallo.</returns>
        public static string? GetRegValue(string key, string? dValue = "")
        {
            try {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    RegistryKey? la_key = The_key();

                    if (la_key != null) {
                        byte[]? byteValue = SetNullValue(la_key.GetValue(key));

                        SimpleAES? aes_crypt = new();

                        string? la_resp = byteValue != null ? aes_crypt.DecryptFromBytes(byteValue) : dValue;
                        la_key.Close();
                        aes_crypt = null;

                        return la_resp;
                    }
                }
                return null;
            } catch {
                return string.Empty;
            }
        }

        /// <summary>
        /// Recupera los parámetros del sistema almacenados en el Registro de Windows.
        /// </summary>
        /// <returns>Cadena de texto con la lista de parámetros.</returns>
        public static string? GetSysParams()
        {
            return GetRegValue("db_params");
        }

        #region Guardar configuraciones
        /// <summary>
        /// Método universal para la escritura de valores al Registro de Windows.
        /// </summary>
        /// <param name="nom_val">Nombre del valor que será escrito.</param>
        /// <param name="val_val">Valor asignado.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public static bool SetRegValue(string nom_val, string? val_val)
        {
            bool la_resp = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                RegistryKey? la_key = The_key();

                if (la_key != null) {
                    SimpleAES? aes_crypt = new();

                    try {
                        la_key.SetValue(nom_val, aes_crypt.EncryptToBytes(val_val));
                        la_resp = true;
                    } catch {
                        la_resp = false;
                    } finally  {
                        la_key.Close();
                    }
                }
            }

            return la_resp;
        }

        public static bool SetDBConSettings(MainSettings msettings)
        {
            bool la_resp;
            try {
                SetRegValue("db_server", msettings.Db_server);
                SetRegValue("db_name", msettings.Db_name);
                SetRegValue("db_user", msettings.Db_user);
                SetRegValue("db_pass", msettings.Db_pass);
                la_resp = true;
            } catch {
                la_resp = false;
            }
            return la_resp;
        }

        /// <summary>
        /// Guarda todas las configuraciones del sistema en el Registro de Windows.
        /// </summary>
        /// <param name="msettings">Objeto MainSettings con las configuraciones generales que se guardarán.</param>
        /// <param name="csettings">Objeto ClockSettings con las configuraciones del Reloj que se guardarán.</param>
        /// <returns>True si las configuraciones fueron escritas correctamente.</returns>
        public static bool SetRegSettings(MainSettings msettings, ClockSettings? csettings)
        {
            bool la_resp = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                RegistryKey? la_key = The_key();

                if (la_key != null) {
                    SimpleAES? aes_crypt = new();

                    try {
                        la_key.SetValue("path_tmp", aes_crypt.EncryptToBytes(msettings.Path_tmp));
                        la_key.SetValue("logo", aes_crypt.EncryptToBytes(msettings.Logo));
                        la_key.SetValue("ftp_url", aes_crypt.EncryptToBytes(msettings.Ftp_url));
                        la_key.SetValue("ftp_port", aes_crypt.EncryptToBytes(msettings.Ftp_port));
                        la_key.SetValue("ftp_user", aes_crypt.EncryptToBytes(msettings.Ftp_user));
                        la_key.SetValue("ftp_pass", aes_crypt.EncryptToBytes(msettings.Ftp_pass));
                        la_key.SetValue("employees_host", aes_crypt.EncryptToBytes(msettings.Employees_host));
                        la_key.SetValue("websocket_enabled", aes_crypt.EncryptToBytes(msettings.Websocket_enabled.ToString()));
                        la_key.SetValue("websocket_host", aes_crypt.EncryptToBytes(msettings.Websocket_host));
                        la_key.SetValue("websocket_port", aes_crypt.EncryptToBytes(msettings.Websocket_port));
                        la_key.SetValue("pusher_app_id", aes_crypt.EncryptToBytes(msettings.Pusher_app_id));
                        la_key.SetValue("pusher_key", aes_crypt.EncryptToBytes(msettings.Pusher_key));
                        la_key.SetValue("pusher_secret", aes_crypt.EncryptToBytes(msettings.Pusher_secret));
                        la_key.SetValue("pusher_cluster", aes_crypt.EncryptToBytes(msettings.Pusher_cluster));
                        la_key.SetValue("event_name", aes_crypt.EncryptToBytes(msettings.Event_name));
                        la_key.SetValue("mailEnabled", aes_crypt.EncryptToBytes(msettings.MailEnabled.ToString()));
                        la_key.SetValue("mailServer", aes_crypt.EncryptToBytes(msettings.MailServer));
                        la_key.SetValue("mailPort", aes_crypt.EncryptToBytes(msettings.MailPort));
                        la_key.SetValue("mailUser", aes_crypt.EncryptToBytes(msettings.MailUser));
                        la_key.SetValue("mailPass", aes_crypt.EncryptToBytes(msettings.MailPass));
                        la_key.SetValue("mailRecipient", aes_crypt.EncryptToBytes(msettings.MailRecipient));
                        la_key.SetValue("usePusher", aes_crypt.EncryptToBytes(msettings.UsePusher.ToString()));
                        la_key.SetValue("signalRHubUrl", aes_crypt.EncryptToBytes(msettings.SignalRHubUrl));
                        la_key.SetValue("signalRHubName", aes_crypt.EncryptToBytes(msettings.SignalRHubName));
                        la_key.SetValue("signalRMethodName", aes_crypt.EncryptToBytes(msettings.SignalRMethodName));
                        la_key.SetValue("signalRAdminMethodName", aes_crypt.EncryptToBytes(msettings.SignalRAdminMethodName));
                        la_key.SetValue("db_server", aes_crypt.EncryptToBytes(msettings.Db_server));
                        la_key.SetValue("db_name", aes_crypt.EncryptToBytes(msettings.Db_name));
                        la_key.SetValue("db_user", aes_crypt.EncryptToBytes(msettings.Db_user));
                        la_key.SetValue("db_pass", aes_crypt.EncryptToBytes(msettings.Db_pass));
                        la_key.SetValue("seqUrl", aes_crypt.EncryptToBytes(msettings.SeqUrl));
                        la_key.SetValue("seqApiKey", aes_crypt.EncryptToBytes(msettings.SeqApiKey));

                        if (GlobalVars.VTAttModule == 1) {
                            la_key.SetValue("clock_office", aes_crypt.EncryptToBytes(csettings.clock_office.ToString()));
                            la_key.SetValue("clock_user", aes_crypt.EncryptToBytes(csettings.clock_user));
                            la_key.SetValue("clock_pass", aes_crypt.EncryptToBytes(csettings.clock_pass));
                            la_key.SetValue("clock_timezone", aes_crypt.EncryptToBytes(csettings.clock_timezone));
                        }

                        la_resp = true;
                    } catch(Exception ex) {
                        log.Warn("Error al guardar configuraciones en el registro: " + ex.Message);
                        la_resp = false;
                    }
                    finally {
                        la_key.Close();
                    }
                }
            }
            return la_resp;
        }

        /// <summary>
        /// Guarda la URL del Servicio Web correspondiente en el registro, encriptada.
        /// </summary>
        /// <param name="the_url">URL del Servicio Web.</param>
        /// <returns>True si la operación fue exitosa.</returns>
        public static bool SetWSSettings(string the_url)
        {
            return SetRegValue("ws_url", the_url);
        }

        /// <summary>
        /// Almacena los parámetros del sistema en el Registro de Windows.
        /// </summary>
        /// <param name="params_str">Cadena de texto con la lista de parámetros.</param>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        public static bool SaveSysParams(string params_str)
        {
            return SetRegValue("db_params", params_str);
        }

        /// <summary>
        /// Almacena en el Registro de Windows la fecha y hora de la última sincronización exitosa con el Servicio Web.
        /// </summary>
        /// <param name="el_timestamp">Objeto DateTime que se guardará.</param>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        public static bool SaveLastSync(DateTime el_timestamp)
        {
            return SetRegValue("last_sync", (el_timestamp.ToString("yyyyMMddHHmmss")));
        }

        /// <summary>
        /// Guarda la información de la oficina de trabajo en el Registro de Windows.
        /// </summary>
        /// <param name="la_office">Objeto con la información de la oficina para guardar.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public static bool SaveOffRegData(OfficeData la_office)
        {
            bool r1 = SetRegValue("office_id", la_office.Offid.ToString());
            bool r2 = SetRegValue("office_name", la_office.Offname);
            bool r3 = SetRegValue("office_desc", la_office.Offdesc);

            return (r1 && r2 && r3);
        }
        #endregion
    }
}
