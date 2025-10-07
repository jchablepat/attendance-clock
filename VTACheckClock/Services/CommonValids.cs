using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.Services
{
    class CommonValids
    {
        private const string V = @"^[\b";

        #region Validaciones misceláneas

        /// <summary>
        /// Valida la entrada de un campo, según la regla seleccionada.
        /// </summary>
        /// <param name="el_char">El caracter que se validará.</param>
        /// <param name="el_tipo">Regla de validación.</param>
        /// <returns>True si el caracter es válido.</returns>
        public static bool ValidaTecla(char el_char, int el_tipo)
        {
            bool la_resp = false;

            if (el_tipo == 0) {
                la_resp = true;
                goto salte;
            }

            string el_pattern = V; //Retroceso (se añade a todas las validaciones).
            string hisp_letters = @"A-Za-zÑñáÁéÉíÍóÓúÚ´üÜ¨"; //Letras hispanas (se añade en algunas validaciones).
            string illegalChars = @"^[^\x00-\x07\x09-\x1f?*\"";<>|/]+$"; //Caracteres inválidos para rutas del sistema de archivos Windows.

            switch (el_tipo) {
                case 11:
                    el_pattern += @"A-Za-z\d]$"; //Letras inglesas y dígitos.
                    break;

                case 10:
                    return (((new Regex(illegalChars)).IsMatch(el_char.ToString()))); //Devuelve TRUE si encuentra caracteres inválidos para rutas del sistema de archivos Windows.

                case 9:
                    el_pattern += @"\d-]$"; //Dígitos y símbolo de negativo.
                    break;

                case 8:
                    el_pattern += @"A-Za-z\d.]$"; //Letras inglesas, dígitos y punto.
                    break;

                case 7:
                    el_pattern += hisp_letters + @"\d. ""@]$"; //Letras hispanas, dígitos, punto, espacio, comillas dobles y arroba.
                    break;

                case 6:
                    el_pattern += hisp_letters + @"\d]$"; //Letras hispanas, dígitos.
                    break;

                case 5:
                    el_pattern += hisp_letters + @"\d ]$"; //Letras hispanas, dígitos y espacio.
                    break;

                case 4:
                    el_pattern += @"\d]$"; //Sólo dígitos.
                    break;

                case 3:
                    el_pattern += @"\d.]$"; //Dígitos y punto.
                    break;

                case 2:
                    el_pattern += hisp_letters + @" ]$"; //Letras hispanas y espacio.
                    break;

                case 1:
                    el_pattern += hisp_letters + @"\d. ]$"; //Letras hispanas, dígitos, punto y espacio.
                    break;

                default:
                    el_pattern += hisp_letters + @"\d. ]$"; //Letras hispanas, dígitos, punto y espacio.
                    break;
            }

            Regex los_chars = new(el_pattern);
            if (los_chars.IsMatch(el_char.ToString())) {
                la_resp = true;
                goto salte;
            }

        salte:
            return la_resp;
        }

        #endregion

        #region Validaciones de sesión

        /// <summary>
        /// Valida las credenciales de inicio de sesión.
        /// </summary>
        /// <param name="el_login">Objeto con la información de inicio de sesión.</param>
        /// <param name="ws_url">URL del Servicio Web que se utilizará en caso que no se pueda o no se desee utilizar la almacenada en el entorno de la aplicación (opcional).</param>
        /// <returns>True si las credenciales son válidas.</returns>
        public static bool ValidaLogin(SessionData el_login, string? ws_url = null)
        {
            try {
                DataTable? dt = CommonProcs.ObjToDt(CommonObjs.BeBlunt(CommonProcs.ValidateSession(el_login)));
                ScantResponse response = CommonProcs.DtToList<ScantResponse>(dt)[0];

                return response.Ack;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Invoca la ventana de inicio de sesión y valida las credenciales proporcionadas.
        /// </summary>
        /// <param name="el_privilege">El privilegio con que debe contar el usuario que proporcionará sus claves.</param>
        /// <param name="ws_url">URL del Servicio Web que se utilizará en caso que no se pueda o no se desee utilizar la almacenada en el entorno de la aplicación (opcional).</param>
        /// <returns>True si el inicio de sesión fue correcto.</returns>
        public static bool InvokeLogin(int el_privilege, string? ws_url = null)
        {
            //using (frmLogin el_login = new frmLogin(el_privilege, ws_url))
            //{
            //    SessionData tmpstor_session = GlobalVars.mySession;
            //    int[] tmpstor_usrprivs = GlobalVars.UserPrivileges;

            //    GlobalVars.mySession = null;
            //    GlobalVars.UserPrivileges = null;
            //    el_login.LoginObject = null;
            //    DialogResult el_result = el_login.ShowDialog();

            //    if ((el_result == DialogResult.OK) && (el_login.LoginObject != null))
            //    {
            //        GlobalVars.mySession = el_login.LoginObject;
            //        CommonProcs.SetUsrPrivs();

            //        return true;
            //    } else {
            //        GlobalVars.mySession = tmpstor_session;
            //        GlobalVars.UserPrivileges = tmpstor_usrprivs;

            //        return false;
            //    }
            //}
            return true;
        }


        #endregion

        #region Validaciones individuales
        /// <summary>
        /// Valida la conexión con el Servicio Web solicitado.
        /// </summary>
        /// <param name="ws_url">URL del Servicio Web que se utilizará en caso que no se pueda o no se desee utilizar la almacenada en el entorno de la aplicación (opcional).</param>
        /// <returns>True si el servicio web se encuentra en línea y responde.</returns>
        public static bool ValidWSConnection(string? ws_url = null)
        {
            try {
                DataTable? dt = CommonProcs.ObjToDt(CommonProcs.WebServiceOnline());
                ScantResponse response = CommonProcs.DtToList<ScantResponse>(dt)[0];

                return response.Ack;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Verifica que los objetos de configuración obtenidos del Registro de Windows no contengan valores nulos o vacíos.
        /// </summary>
        /// <param name="msettings">Objeto MainSettings que se evaluará.</param>
        /// <param name="csettings">Objeto ClockSettings que se evaluará.</param>
        /// <returns>True si la información es válida. False de lo contrario.</returns>
        public static bool ValidSettingsObjs(MainSettings? msettings, ClockSettings? csettings = null)
        {
            if ((msettings == null) || ((csettings == null) && (GlobalVars.VTAttModule == 1)))
            {
                return false;
            }

            List<PropertyInfo> las_props = [.. msettings.GetType().GetProperties()];

            string[] nullableProperties = [ 
                "Employees_host", "Websocket_enabled", "Websocket_host", "Websocket_port", 
                "Pusher_key", "Pusher_cluster", "Pusher_app_id", "Pusher_secret", "Event_name", 
                "Ws_url", "Db_server", "Db_name", "Db_user", "Db_pass", "Logo", "mailEnabled",
                "MailServer", "MailPort", "MailUser", "MailPass", "MailRecipient",
                "UsePusher", "SignalRHubUrl", "SignalRHubName", "SignalRMethodName", "SignalRApiKey",
                "SeqUrl", "SeqApiKey"
            ];

            foreach (PropertyInfo la_prop in las_props)
            {
                //Ignore specified properties and CONTINUE with next 
                if (Array.IndexOf(nullableProperties, la_prop.Name) >= 0) {
                    continue;
                }

                var to_check = la_prop.GetValue(msettings);
                if ((to_check == null) || ((to_check is string) && string.IsNullOrWhiteSpace(to_check.ToString())))
                {
                    return false;
                }
            }

            if (GlobalVars.VTAttModule == 1)
            {
                las_props = [.. csettings.GetType().GetProperties()];

                string[] nullProperties = ["clock_uuid", "clock_timezone"];

                foreach (PropertyInfo la_prop in las_props)
                {
                    if (Array.IndexOf(nullProperties, la_prop.Name) >= 0) {
                        continue;
                    }

                    var to_check = la_prop.GetValue(csettings);
                    if ((to_check == null) || ((to_check is string) && string.IsNullOrWhiteSpace(to_check.ToString())))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Verifica si existe la ruta de trabajo para el sistema y si se tienen permisos suficientes en ella. Si no existe, intenta crearla.
        /// </summary>
        /// <param name="the_path">La ruta completa de la carpeta temporal.</param>
        /// <returns>True si la ruta existe o fue creada con éxito y se puede leer y escribir en ella.</returns>
        public static bool ValidTempPath(string? the_path)
        {
            if (the_path != null && !Directory.Exists(the_path))
            {
                try {
                    Directory.CreateDirectory(the_path);
                } catch {
                    //MessageBox.Show("No se encuentra la ruta de la carpeta temporal y no se pudo crear.", "Carpeta de trabajo inaccesible", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return false;
                }
            }

            try {
                FileStream fs = File.Create((the_path + @"\test.tst"), 1, FileOptions.DeleteOnClose);
                fs.Close();
                return true;
            } catch {
                Show(null, "Carpeta de trabajo restringida", "No se tiene acceso de escritura a la carpeta temporal.", MessageBoxButtons.Ok);
                return false;
            }
        }

        /// <summary>
        /// Valida la conexión al servidor FTP y efectúa una operación de escritura de archivos.
        /// </summary>
        /// <param name="the_settings">Objeto con la información de conexión al servidor FTP.</param>
        /// <returns>True si la conexión al servidor fue exitosa y se consiguió transferir y eliminar un archivo.</returns>
        public static bool ValidFTPConn(MainSettings the_settings)
        {
            return true;
        }

        /// <summary>
        /// Valida que la oficina guardada en la configuración sea correcta.
        /// </summary>
        /// <param name="the_settings">Objeto con las configuraciones requeridas.</param>
        /// <param name="ws_url">URL del Servicio Web que se utilizará en caso que no se pueda o no se desee utilizar la almacenada en el entorno de la aplicación (opcional).</param>
        /// <returns>True si la oficina especificada es válida.</returns>
        public static bool ValidOffice(ClockSettings? the_settings, string? ws_url = null)
        {
            if (GlobalVars.VTAttModule == 1)
            {
                if (GlobalVars.BeOffline)
                {
                    GlobalVars.this_office = RegAccess.GetOffRegData();

                    return (GlobalVars.this_office.Offid > 0);
                } else {
                    DataTable? dt = CommonProcs.ObjToDt(CommonProcs.CheckOffice(new ScantRequest { Question = the_settings.clock_office.ToString() }));

                    if (dt == null || CommonProcs.IsDtVoid(dt)) {
                        Show(null, "Oficina inválida", "La oficina configurada para este Reloj Checador no es válida.", MessageBoxButtons.Ok);

                        return false;
                    } else {
                        GlobalVars.this_office = CommonProcs.DtToList<OfficeData>(dt)[0];
                        RegAccess.SaveOffRegData(GlobalVars.this_office);

                        return true;
                    }
                }
            }

            //if (GlobalVars.VTAttModule == 2)
            //{
            //    GlobalVars.this_office = GlobalVars.managmntoff;

            //    return true;
            //}

            return true;
        }

        /// <summary>
        /// Valida que las credenciales de inicio de sesión para la aplicación sean correctas.
        /// </summary>
        /// <param name="the_settings">Objeto con las configuraciones requeridas.</param>
        /// <param name="ws_url">URL del Servicio Web que se utilizará en caso que no se pueda o no se desee utilizar la almacenada en el entorno de la aplicación (opcional).</param>
        /// <returns>True si las credenciales almacenadas son correctas.</returns>
        public static bool ValidClockLogin(ClockSettings? the_settings, string? ws_url = null)
        {
            SessionData my_login = new() {
                usrname = the_settings.clock_user,
                passwd = the_settings.clock_pass,
                accpriv = GlobalVars.ClockStartPriv
            };

            return ValidaLogin(my_login, ws_url);
        }

        /// <summary>
        /// Verifica la correcta generación de la instancia del manejador del caché.
        /// </summary>
        /// <returns>True si el caché se generó con éxito. False de lo contrario.</returns>
        public static bool ValidCacheGen(out string error_str)
        {
            error_str = "";
            if (GlobalVars.VTAttModule == 1) {
                GlobalVars.AppCache = new CacheMan();

                if ((GlobalVars.AppCache == null) || (!GlobalVars.AppCache.CreateSuccess)) {
                    Show(null, "Fallo al crear caché local", "Fallo al crear el caché local.\n\nPor favor, comuníquese con el administrador del sistema.", MessageBoxButtons.Ok);
                    return false;
                }

                bool emps_registered = !GlobalVars.BeOffline || (GlobalVars.AppCache.OldEmployeesRecords > 0);

                if (!emps_registered) {
                    error_str = "El modo «Fuera de línea» está activado y no se ha encontrado información de colaboradores en el caché local. Por favor, verifique que cuenta con una conexión activa a Internet y que no se invocó el modo «Fuera de línea» desde la línea de comandos y reintente.\n\nLa aplicación terminará ahora.";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Valida si existe conexión a Internet por medio de una operación de PING a los servidores DNS públicos de Google.
        /// </summary>
        /// <returns>True si la operación de PING fue exitosa. False de lo contrario.</returns>
        public static bool ValidInternetConn()
        {
            try {
                return new Ping().Send("8.8.8.8").Status == IPStatus.Success;
            }  catch {
                return false;
            }
        }

        /// <summary>
        /// Valida si existe conexión a Internet por medio de una operación de PING a los servidores DNS públicos de Google de manera asíncrona, lo que permite que la ejecución continúe sin bloquear el hilo principal..
        /// </summary>
        /// <returns>True si la operación de PING fue exitosa. False de lo contrario.</returns>
        public static async Task<bool> ValidInternetConnAsync()
        {
            try {
                // Intentar múltiples destinos por si alguno está bloqueado
                string[] hosts = ["8.8.8.8", "1.1.1.1", "208.67.222.222"];

                foreach (var host in hosts)
                {
                    try {
                        PingReply reply = await new Ping().SendPingAsync(host, 1000);
                        //$"Tiempo de respuesta: {reply.RoundtripTime} ms";

                        if (reply.Status == IPStatus.Success)
                            return true;
                    } catch {
                        // Continuar con el siguiente host
                        return false;
                    }
                }

                return false;
            } catch {
                return false;
            }
        }
        #endregion

        #region Conjunto de Validaciones
        /// <summary>
        /// La validación de inicio de las aplicaciones.
        /// </summary>
        /// <returns>True si todas las validaciones se completaron con éxito. False de lo contrario.</returns>
        public static bool ValidStartup(out string error_str)
        {
            error_str = string.Empty;
            try {
                bool retry = true;
                bool go_on = false;

                //while (retry) {
                    //retry = false;
                    go_on = RegAccess.GetRegSettings(out GlobalVars.mainSettings, out GlobalVars.clockSettings);

                    if (!go_on || (GlobalVars.mainSettings == null) || ((GlobalVars.clockSettings == null) && (GlobalVars.VTAttModule == 1))) {
                        ReinstallMe(true, out error_str);
                    }
                //}

                if (!go_on) return false;

                retry = true;
                go_on = false;

                //while (retry) {
                    retry = false;
                    go_on = ValidSettingsObjs(GlobalVars.mainSettings, GlobalVars.clockSettings);

                    if (!go_on) {
                        retry = ReinstallMe(true, out error_str);
                    }
                //}

                if (!go_on) return false;

                retry = true;
                go_on = false;

                //while (retry) {
                    retry = false;
                    go_on = ValidTempPath(GlobalVars.mainSettings.Path_tmp);

                    if (!go_on) {
                        retry = ReinstallMe(true, out error_str);
                    }
                //}

                if (!go_on) return false;

                CommonProcs.SetOfflineMode();

                if (GlobalVars.BeOffline && GlobalVars.VTAttModule == 2)
                {
                    error_str = "No se ha detectado conexión a Internet. Esta aplicación no puede continuar sin una conexión activa a Internet.\n\nLa aplicación terminará ahora.";
                    return false;
                }

                if ((!GlobalVars.BeOffline) || (GlobalVars.VTAttModule == 2))
                {
                    retry = true;
                    go_on = false;

                    //while (retry) {
                        retry = false;
                        go_on = true; // ValidWSConnection(GlobalVars.mainSettings.ws_url);

                        if (!go_on) {
                            //retry = ReinstallMe(true);
                        }
                    //}

                    if (!go_on) return false;

                    retry = true;
                    go_on = false;

                    //while (retry) {
                        retry = false;

                        go_on = GlobalVars.VTAttModule switch
                        {
                            2 => ValidFTPConn(GlobalVars.mainSettings),
                            1 => ValidClockLogin(GlobalVars.clockSettings),
                            _ => false,
                        };

                        if (!go_on) {
                            //retry = ReinstallMe(true);
                        }
                    //}

                    if (!go_on) return false;
                }

                retry = true;
                go_on = false;

                //while (retry) {
                    retry = false;
                go_on = ValidOffice(GlobalVars.clockSettings);

                    if (!go_on) {
                        //retry = ReinstallMe(true);
                    }
                //}

                if (!go_on) return false;

                go_on = ValidCacheGen(out error_str);
                return go_on;
            } catch(Exception ex) {
                error_str = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Valida que los valores introducidos en las pantallas de configuración del sistema sean correctos.
        /// </summary>
        /// <param name="msettings">Objeto que encapsula la información de configuración general.</param>
        /// <param name="csettings">Objeto que encapsula la información de configuración del Reloj Checador.</param>
        /// <param name="error_code">Variable donde se almacenará el código de error generado durante las validaciones.</param>
        /// <param name="ws_url">URL que se utilizará para conectar con el Serivico Web.</param>
        /// <returns>True si todas las validaciones fueron exitosas.</returns>
        public static bool ValidSettings(MainSettings msettings, ClockSettings? csettings, out int error_code, string? ws_url = null)
        {
            GlobalVars.BeOffline = false;

            error_code = 4;

            if (!ValidTempPath(msettings.Path_tmp))
            {
                return false;
            }

            error_code--;

            if (!ValidFTPConn(msettings))
            {
                return false;
            }

            error_code--;

            if (GlobalVars.VTAttModule == 1)
            {
                if (!ValidOffice(csettings, ws_url))
                {
                    return false;
                }

                error_code--;

                if (!ValidClockLogin(csettings, ws_url))
                {
                    return false;
                }

                error_code--;
            } else {
                error_code -= 2;
            }

            return true;
        }
        #endregion

        #region Invocación de pantallas de configuración

        /// <summary>
        /// Invoca las pantallas de configuración del sistema necesarias, según se indica en el parámetro, y guarda las configuraciones nuevas.
        /// </summary>
        /// <param name="from_ws">Indica si la configuración deberá ejecutarse desde la pantalla de configuración del WebService o no.</param>
        /// <param name="ws_url">El URL del WebService (opcional).</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public static bool ReinstallMe(bool from_ws, out string error_str, string? ws_url = null)
        {
            error_str = "";
            CommonProcs.SetOfflineMode();

            if (GlobalVars.BeOffline) {
                string err_msg_part_1 = (GlobalVars.DoReinstall) ? "Se solicitó la reconfiguración de la aplicación a través de la línea de comandos" : "Se han encontrado problemas con la configuración de la aplicación, por lo que necesita ser reconfigurada";
                string err_msg_part_2 = (GlobalVars.OfflineInvoked) ? "se especificó el argumento «-go_offline» en línea de comandos" : "no ha sido detectada ninguna conexión en este equipo";

                error_str = "Sin conexión a Internet." + err_msg_part_1 + ". Sin embargo, la reconfiguración de la aplicación requiere una conexión activa a Internet, pero " + err_msg_part_2 + ".\n\nLa aplicación no puede continuar y terminará ahora.";

                return false;
            }

            //ButtonResult be_result = (GlobalVars.DoReinstall) ? ButtonResult.Yes : ShowPrompt("Configuración errónea o incompleta", "No se ha llevado a cabo la configuración inicial de la aplicación o la misma se encuentra incompleta o dañada.\n\nA continuación, se le solicitará la información faltante, por lo que antes de continuar deberá tenerla a la mano o comunicarse con el administrador del sistema.\n\n¿Desea continuar?");

            //if (be_result == ButtonResult.No) {
                //return false;
            //}

            //if (from_ws)
            //{
            //    using (frmWSPrompt wssetts = new frmWSPrompt())
            //    {
            //        DialogResult el_result = wssetts.ShowDialog();

            //        if ((el_result == DialogResult.OK) && ((wssetts.ws_url.Trim()).Length > 0))
            //        {
            //            if (!RegAccess.SetWSSettings(wssetts.ws_url.Trim()))
            //            {
            //                //MessageBox.Show("Ocurrió un problema al guardar la configuración del Servicio Web. Favor de contactar al administrador del sistema.", "Fallo al guardar la configuración", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            //                return false;
            //            } else {
            //                ws_url = wssetts.ws_url;
            //                //MessageBox.Show("La configuración de conexión al Servicio Web se ha guardado exitosamente.", "Servicio Web configurado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //            }
            //        } else {
            //            return false;
            //        }
            //    }
            //}

            //ButtonResult this_result = await ShowPrompt("Claves de administrador requeridas", "Deberá iniciar sesión como administrador para completar la configuración siguiente, ¿desea continuar?");

            //if (this_result == ButtonResult.No) {
                //return false;
            //}

            //using (frmSettings setts = new frmSettings(ws_url))
            //{
            //    DialogResult el_result = setts.ShowDialog();

            //    if ((el_result == DialogResult.OK) && (setts.setupok))
            //    {
            //        if (!(RegAccess.SetRegSettings(setts.m_settings, setts.c_settings)))
            //        {
            //            //MessageBox.Show("Ocurrió un problema al guardar las configuraciones. Favor de contactar al administrador del sistema.", "Fallo al guardar la configuración", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            //            return false;
            //        }
            //    } else {
            //        return false;
            //    }
            //}

            //Show(null, "Configuración actualizada", "La configuración ha sido almacenada correctamente.\n\n\nLa aplicación se reiniciará ahora.", MessageBoxButtons.Ok);
            RegAccess.GetRegSettings(out GlobalVars.mainSettings, out GlobalVars.clockSettings);

            return true;
        }

        #endregion
    }
}
