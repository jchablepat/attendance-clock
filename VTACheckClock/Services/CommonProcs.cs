using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using VTACheckClock.DBAccess;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;
using Avalonia.Media.Imaging;
using NLog;
using ICSharpCode.SharpZipLib.BZip2;
using System.Globalization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VTACheckClock.Services
{
    class CommonProcs
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        #region Métodos para compresión y encriptación de cadenas de texto
        /// <summary>
        /// Codifica una cadena de texto a Base64.
        /// </summary>
        /// <param name="plainText">Cadena a codificar.</param>
        /// <returns>Cadena codificada.</returns>
        public static string StrToBase64(string? plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText ?? "");
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Decodifica una cadena en Base64 a texto.
        /// </summary>
        /// <param name="base64EncodedData">Cadena a decodificar.</param>
        /// <returns>Cadena decodificada.</returns>
        public static string Base64ToStr(string? base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData ?? "");
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Comprime una cadena de texto utilizando GZip.
        /// </summary>
        /// <param name="text">Cadena a comprimir.</param>
        /// <returns>Cadena comprimida.</returns>
        public static string ZipString(string? text)
        {
            byte[] buffer = Encoding.Unicode.GetBytes(text ?? "");
            MemoryStream ms = new();

            using (GZipStream zip = new(ms, CompressionMode.Compress, true))
            {
                zip.Write(buffer, 0, buffer.Length);
            }

            ms.Position = 0;

            byte[] compressed = new byte[ms.Length];
            ms.Read(compressed, 0, compressed.Length);

            byte[] gzBuffer = new byte[compressed.Length + 4];
            Buffer.BlockCopy(compressed, 0, gzBuffer, 4, compressed.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gzBuffer, 0, 4);

            return Convert.ToBase64String(gzBuffer);
        }

        /// <summary>
        /// Descomprime una cadena de texto comprimida con GZip.
        /// </summary>
        /// <param name="compressedText">Cadena a descomprimir.</param>
        /// <returns>Cadena descomprimida.</returns>
        public static string UnZipString(string? compressedText)
        {
            if (string.IsNullOrEmpty(compressedText)) {
                return string.Empty;
            }

            byte[] gzBuffer = Convert.FromBase64String(compressedText);

            using MemoryStream ms = new();
            int msgLength = BitConverter.ToInt32(gzBuffer, 0);
            ms.Write(gzBuffer, 4, gzBuffer.Length - 4);

            byte[] buffer = new byte[msgLength];

            ms.Position = 0;
            using (GZipStream zip = new(ms, CompressionMode.Decompress))
            {
                zip.Read(buffer, 0, buffer.Length);
            }

            var result = Encoding.Unicode.GetString(buffer, 0, buffer.Length);
            return result;
        }

        /// <summary>
        /// Encripta y desencripta una cadena de texto, utilizando la clase SimpleAES incluída.
        /// </summary>
        /// <param name="la_input">Texto a encriptar/desencriptar.</param>
        /// <param name="encriptar">True para encriptar, false para desencriptar.</param>
        /// <returns>Cadena encriptada/desencriptada.</returns>
        public static string? EnDeCryptTxt(string? la_input, bool encriptar)
        {
            if (string.IsNullOrEmpty(la_input) && (!encriptar)) {
                return string.Empty;
            }

            try {
                SimpleAES? aes_crypt = new();
                string? la_resp = encriptar ? aes_crypt.EncryptToString(la_input!) : aes_crypt.DecryptString(la_input!);
                aes_crypt = null;

                return la_resp;
            } catch(Exception ex) {
                string encdectxt = (encriptar) ? "encriptar" : "desencriptar";
                log.Error(ex, "El texto a " + encdectxt + " no es válido, favor de rectificar."); //"Datos inválidos"

                return null;
            }
        }

        /// <summary>
        /// Encapsula y desencapsula las cadenas que se generan para comunicación con los Servicios Web o las canalizaciones.
        /// </summary>
        /// <param name="str">Cadena de entrada.</param>
        /// <param name="enc_dec">True para encapsular, false para desencapsular.</param>
        /// <returns>La cadena de texto encapsulada/desencapsulada.</returns>
        public static string? EnDeCapsulateTxt(string? str, bool enc_dec)
        {
            if (enc_dec)  {
                return ZipString(EnDeCryptTxt(str, true));
            }

            else
            {
                return EnDeCryptTxt(UnZipString(str), false);
            }
        }
        #endregion

        #region Métodos para manejo de cadenas de texto
        /// <summary>
        /// Convierte el primer caracter de la cadena de entrada en mayúscula y el resto de la cadena en minúsculas.
        /// </summary>
        /// <param name="str">Cadena de entrada.</param>
        /// <returns>Cadena con el formato descrito.</returns>
        public static string UpperFirst(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            return str.First().ToString().ToUpper() + str[1..].ToLower();
        }

        /// <summary>
        /// Convierte una cadena de texto en fecha, exclusivamente si contiene el formato: yyyyMMddHHmmss.
        /// </summary>
        /// <param name="la_cadena">Cadena de texto original.</param>
        /// <returns>Objeto DateTime final.</returns>
        public static DateTime FromFileString(string? la_cadena)
        {
            DateTime dateTime;
            try {
                if (!string.IsNullOrWhiteSpace(la_cadena)) {
                    int date_year = int.Parse(la_cadena[..4]);
                    int date_month = int.Parse(la_cadena.Substring(4, 2));
                    int date_day = int.Parse(la_cadena.Substring(6, 2));
                    int date_hour = int.Parse(la_cadena.Substring(8, 2));
                    int date_minute = int.Parse(la_cadena.Substring(10, 2));
                    int date_second = int.Parse(la_cadena.Substring(12, 2));

                    dateTime = new DateTime(date_year, date_month, date_day, date_hour, date_minute, date_second);
                    return dateTime;
                    
                }
                return DateTime.MinValue;
            }
            catch {
                return DateTime.MinValue;
            }
        }
        #endregion

        #region Métodos para manejo de tipos y conversión entre clases, DataTables y JSON
        /// <summary>
        /// Obtiene la información de las propiedades de un objeto.
        /// </summary>
        /// <param name="el_obj">Objeto a analizar.</param>
        /// <returns>Colección de propiedades.</returns>
        public static PropertyInfo[]? TraeProps(object el_obj)
        {
            if (el_obj == null) {
                return null;
            }

            return el_obj.GetType().GetProperties();
        }

        /// <summary>
        /// Convierte un solo objeto en DataTable.
        /// </summary>
        /// <param name="el_obj">El objeto a convertir.</param>
        /// <returns>Objeto DataTable.</returns>
        public static DataTable? ObjToDt(object? el_obj)
        {
            if (el_obj == null) {
                return null;
            }

            List<object> la_list = [
                el_obj
            ];

            DataTable? dt = ListToDt(la_list);

            return dt;
        }

        /// <summary>
        /// Convierte una Lista de objetos en DataTable.
        /// Invocar como: ListToDt(List<T>.Cast<object>().ToList());
        /// </summary>
        /// <param name="los_objs">La lista que se convertirá.</param>
        /// <returns>Objeto DataTable.</returns>
        public static DataTable? ListToDt(List<object> los_objs)
        {
            if ((los_objs == null) || (los_objs.Count == 0)) {
                return null;
            }

            PropertyInfo[]? las_props = TraeProps(los_objs[0]);
            DataTable dt = new();

            foreach (PropertyInfo prop in las_props)
            {
                dt.Columns.Add(prop.Name);
            }

            foreach (object el_obj in los_objs)
            {
                DataRow dr = dt.NewRow();

                foreach (PropertyInfo prop in las_props)
                {
                    dr[prop.Name] = prop.GetValue(el_obj);
                }

                dt.Rows.Add(dr);
            }

            return dt;
        }

        /// <summary>
        /// Convierte un DataTable en una lista de objetos T.
        /// </summary>
        /// <typeparam name="T">Tipo de objeto en que se convertirá.</typeparam>
        /// <param name="dt">Objeto DataTable.</param>
        /// <returns>Lista de objetos T.</returns>
        public static List<T>? DtToList<T>(DataTable? dt)
        {
            if ((dt == null) || (dt.Rows.Count == 0))
            {
                return null;
            }

            List<T> data = new() {
                Capacity = dt.Rows.Count
            };

            foreach (DataRow row in dt.Rows)
            {
                T? item = DrToObj<T>(row);
                data.Add(item!);
            }

            return data;
        }

        /// <summary>
        /// Convierte una DataRow en un objeto T.
        /// </summary>
        /// <typeparam name="T">Tipo de objeto en que se convertirá.</typeparam>
        /// <param name="dr">Objeto DataRow.</param>
        /// <returns>Instancia de objeto T.</returns>
        public static T? DrToObj<T>(DataRow dr)
        {
            if ((dr == null) || (dr.ItemArray.Length == 0))
            {
                return default;
            }

            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName) {
                        try {
                            pro.SetValue(obj, Convert.ChangeType(dr[column.ColumnName], pro.PropertyType), null);
                        } catch  {
                            pro.SetValue(obj, null, null);
                        }
                    } else {
                        continue;
                    }
                }
            }

            return obj;
        }

        /// <summary>
        /// Evalúa si una cadena de texto representa un valor DateTime válido y devuelve otra cadena de texto con el formato solicitado,
        /// de acuerdo a las condiciones especificadas en los parámetros.
        /// </summary>
        /// <param name="in_str">Cadena de texto a evaluar.</param>
        /// <param name="form_str">Formato para la cadena de texto de salida.</param>
        /// <param name="accept_min">Determina si se aceptará el valor DateTime.MinValue como salida en caso de errores.</param>
        /// <param name="inval_val">Cadena de texto que se devolverá en caso de fallar las operaciones, si es que no se acepta el valor DateTime.MinValue como respuesta.</param>
        /// <returns></returns>
        public static string ParseValidDT(string in_str, string form_str, bool accept_min = false, string inval_val = "null")
        {
            DateTime la_calcdate = DateTime.MinValue;

            try {
                la_calcdate = NewDateTimeParse(in_str);

                return ((la_calcdate != DateTime.MinValue) || accept_min) ? la_calcdate.ToString(form_str) : inval_val;
            } catch(Exception ex) {
                log.Error(ex, "Error al convertir la cadena '" + in_str + "' a DateTime.");
                return accept_min ? la_calcdate.ToString() : inval_val;
            }
        }

        /// <summary>
        /// Método que convierte una cadena de texto en un objeto DateTime, sin provocar excepciones.
        /// </summary>
        /// <param name="str">Cadena de texto a convertir.</param>
        /// <returns>Objeto DateTime con el valor representado por la cadena de texto de entrada o, de no ser válida ésta, el valor DateTime.MinValue.</returns>
        public static DateTime NewDateTimeParse(string str)
        {
            _ = DateTime.TryParse(str, out DateTime la_resp);

            return la_resp;
        }

        /// <summary>
        /// Método para evaluar si un DataTable es resultado de un fallback (vacío).
        /// </summary>
        /// <param name="dt">Objeto DataTable que será evaluado.</param>
        /// <returns>True si el DataTable es resultado de un fallback. False de lo contrario.</returns>
        public static bool IsDtVoid(DataTable dt)
        {
            return ((dt.TableName == "empty") && (dt.Columns.Count == 1) && (dt.Columns[0].ColumnName == "empty") && (dt.Rows.Count == 1) && (dt.Rows[0][0].ToString() == "\0"));
        }
        #endregion

        #region Métodos para procesar las peticiones de los clientes
        /// <summary>
        /// MÉTODO 0:
        /// Indica que el Web Service está en línea y activo.
        /// </summary>
        /// <returns>Objeto con respuesta positiva.</returns>
        public static ScantResponse WebServiceOnline()
        {
            return CommonObjs.BeBlunt(true);
        }

        /// <summary>
        /// MÉTODO 1:
        /// Valida las credenciales de inicio de sesión.
        /// </summary>
        /// <param name="la_sesion">Información de inicio de sesión.</param>
        /// <returns>Validez de las credenciales de la sesión.</returns>
        public static bool ValidateSession(SessionData my_session)
        {
            DataTable dt = DBMethods.IdentifyAccount(my_session);

            if (dt.Rows.Count == 1) {
                return (dt.Rows[0]["UsrName"].ToString() == my_session.usrname);
            } else {
                return false;
            }
        }

        /// <summary>
        /// MÉTODO 2:
        /// Valida si la oficina solicitada existe y está activa.
        /// </summary>
        /// <param name="la_office">Objeto con el ID de la oficina solicitada.</param>
        /// <returns>Objeto con la respuesta pertinente.</returns>
        public static OfficeData? CheckOffice(ScantRequest la_office)
        {
            OfficeData? la_resp = null;

            try {
                using DataTable dt = DBMethods.GetOffice(int.Parse(la_office.Question ?? "0"));
                if (dt.Rows.Count == 1) {
                    la_resp = new OfficeData {
                        Offid = int.Parse(dt.Rows[0]["OffID"].ToString() ?? "0"),
                        Offname = dt.Rows[0]["OffName"].ToString(),
                        Offdesc = dt.Rows[0]["OffDesc"].ToString()
                    };
                } else {
                    la_resp = null;
                }
            } catch {
                la_resp = null;
            }

            return la_resp;
        }

        /// <summary>
        /// MÉTODO 3:
        /// Recupera el conjunto de privilegios para el usuario especificado.
        /// </summary>
        /// <param name="el_user">Nombre del usuario.</param>
        /// <returns>Conjunto de permisos del usuario.</returns>
        private static DataTable GetUsrPrivs(ScantRequest req)
        {
            return DBMethods.CheckUrPrivilege(req.Question);
        }

        /// <summary>
        /// MÉTODO 4:
        /// Registra la fecha y hora del cliente en la bitácora correspondiente.
        /// </summary>
        /// <param name="req">Objeto con el ID de la oficina cliente y su marca de tiempo.</param>
        /// <returns>Objeto con la respuesta pertinente.</returns>
        public static ScantResponse SyncWatches(ScantRequest req)
        {
            try {
                string[] comm_data = req.Question.Split(['|']);
                int el_result = DBMethods.RegisterClientTime(comm_data[0], int.Parse(comm_data[1]), Convert.ToDateTime(comm_data[2]));

                return CommonObjs.BeBlunt(el_result == 0);
            } catch {
                return CommonObjs.BeBlunt(false);
            }
        }

        public static async Task<ScantResponse> SyncWatchesAsync(ScantRequest req)
        {
            try {
                string[] comm_data = req.Question.Split(['|']);
                int el_result = await DBMethods.RegisterClientTimeAsync(comm_data[0], int.Parse(comm_data[1]), Convert.ToDateTime(comm_data[2]));

                return CommonObjs.BeBlunt(el_result == 0);
            } catch {
                return CommonObjs.BeBlunt(false);
            }
        }

        /// <summary>
        /// Registra un evento por empleado cada vez que pasa la huella en el Lector
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public static async Task<bool> PunchRegisterAsync(ScantRequest req)
        {
            try {
                string[] comm_data = req.Question.Split(['|']);
                DataTable dt = await DBMethods.PunchRegisterAsync(int.Parse(comm_data[0]), int.Parse(comm_data[1]), int.Parse(comm_data[2]), comm_data[3], comm_data[4]);

                bool has_errors = dt.Rows[0]["ErrMess"].ToString() != "None";
                if (has_errors) {
                    log.Warn("Error while sending employee data: " + dt.Rows[0]["ErrMess"].ToString());
                }

                return !has_errors;
            } catch(Exception ex) {
                log.Warn("Error while sending employee data: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// MÉTODO 10:
        /// Recupera la colección de parámetros del sistema y sus valores.
        /// </summary>
        /// <returns>Colección de parámetros.</returns>
        private static List<ParamData> SysParams()
        {
            DataTable dt = GetParams();
            List<ParamData> la_resp = [];

            foreach (DataRow la_row in dt.Rows)
            {
                la_resp.Add(new ParamData {
                    number = int.Parse(la_row["ParamNum"].ToString() ?? "0"),
                    value = la_row["ParamValue"].ToString()
                });
            }

            return la_resp;
        }

        /// <summary>
        /// MÉTODO 11:
        /// Recupera el listado de las oficinas registradas en el sistema.
        /// </summary>
        /// <param name="la_office"></param>
        /// <returns>Listado de oficinas.</returns>
        public static List<OfficeData> GetOffices(ScantRequest la_office)
        {
            List<OfficeData> la_resp = [];
            DataTable dt = DBMethods.GetOffice(int.Parse(la_office.Question ?? "0"));

            foreach (DataRow dr in dt.Rows)
            {
                la_resp.Add(new OfficeData
                {
                    Offid = int.Parse(dr["OffID"].ToString() ?? "0"),
                    Offname = dr["OffName"].ToString(),
                    Offdesc = dr["OffDesc"].ToString()
                });
            }

            return la_resp;
        }

        /// <summary>
        /// MÉTODO 21:
        /// Obtiene la colección de huellas dactilares correspondientes a una oficina específica.
        /// </summary>
        /// <param name="la_request">Objeto con el ID de la oficina que se buscará.</param>
        /// <returns>DataTable con la colección de huellas dactilares para la oficina.</returns>
        public static DataTable GetOfficeFMDs(ScantRequest la_request)
        {
            DataTable dt = DBMethods.GetFMDCollection(Convert.ToInt32(la_request.Question));

            return dt;
        }

        public static async Task<DataTable> GetOfficeFMDsAsync(ScantRequest la_request)
        {
            DataTable dt = await DBMethods.GetFMDCollectionAsync(Convert.ToInt32(la_request.Question));
            return dt;
        }

        /// <summary>
        /// MÉTODO 24:
        /// Obtiene los avisos para la oficina solicitante.
        /// </summary>
        /// <param name="la_req">Objeto con el ID de la oficina que solicita.</param>
        /// <returns>Colección con los avisos correspondientes a la oficina especificada.</returns>
        public static List<NoticeData> GetOfficeNotices(ScantRequest la_req)
        {
            List<NoticeData> la_resp = [];
            _ = int.TryParse(la_req.Question, out int la_office);

            DataTable dt = DBMethods.GetNoticesInfo(la_office);

            foreach (DataRow dr in dt.Rows)
            {
                la_resp.Add(new NoticeData {
                   notid = int.Parse(dr["NotID"].ToString() ?? "0"),
                   nottit = StrToBase64(dr["NotTit"].ToString()),
                   notmsg = StrToBase64(dr["NotMsg"].ToString()),
                   notimg = dr["NotImg"].ToString()
                });
            }

            return la_resp;
        }

        public static async Task<List<NoticeData>> GetOfficeNoticesAsync(ScantRequest la_req)
        {
            List<NoticeData> la_resp = [];
            _ = int.TryParse(la_req.Question, out int la_office);

            DataTable dt = await DBMethods.GetNoticesInfoAsync(la_office);

            foreach (DataRow dr in dt.Rows)
            {
                la_resp.Add(new NoticeData {
                   notid = int.Parse(dr["NotID"].ToString() ?? "0"),
                   nottit = StrToBase64(dr["NotTit"].ToString()),
                   notmsg = StrToBase64(dr["NotMsg"].ToString()),
                   notimg = dr["NotImg"].ToString()
                });
            }

            return la_resp;
        }

        /// <summary>
        /// MÉTODO 25:
        /// Obtiene los últimos registros de asistencia de cada empleado, por oficina.
        /// </summary>
        /// <param name="la_req">Objeto con el ID de la oficina que solicita.</param>
        /// <returns>Colección con los registros de asistencia solicitados.</returns>
        public static DataTable GetLastPunches(ScantRequest la_req, int user_id = 0)
        {
            return DBMethods.RetrieveLastPunches(int.Parse(la_req.Question ?? "0"), user_id);
        }

        public static async Task<DataTable> GetLastPunchesAsync(ScantRequest la_req, int user_id = 0)
        {
            return await DBMethods.RetrieveLastPunchesAsync(int.Parse(la_req.Question ?? "0"), user_id);
        }

        /// <summary>
        /// MÉTODO 54:
        /// Procesa la información de los registros de asistencia de una oficina y los envía a la base de datos.
        /// </summary>
        /// <param name="la_req">Objeto con la información de la oficina solicitante y sus registros de asistencia encapsulada.</param>
        /// <returns>True si la operación tuvo éxito.</returns>
        public static bool SendPunches(ScantRequest la_req)
        {
            string[] requests = la_req.Question.Split('.');

            if (int.TryParse(requests[0], out int la_office))
            {
                string las_punches = UnZipString(requests[1]);

                DataTable punc_data = new() {
                    Columns = { "emp_id", "evt_id", "punc_time", "punc_calc" }
                };

                var punchData = las_punches.Split(GlobalVars.SeparAtor);

                foreach (string str in punchData)
                {
                    string[] pdata = str.Split('|');
                    string client_time = pdata[2].TrimEnd('\0'); // Eliminará todos los caracteres nulos al final de la cadena
                    string calc_time = (pdata.Length == 4 && IsValidDateTime(pdata[3])) ? pdata[3] : "null";

                    // Ajuste por un error al no pasar un Id de Oficina correcta
                    if(pdata[1] != "0") {
                        punc_data.Rows.Add(int.Parse(pdata[0]), int.Parse(pdata[1]), client_time, calc_time);
                    } else {
                        log.Error("Error al procesar el evento '" + pdata[1] + "' del empleado '" + pdata[0] + "' en el horario '"+ client_time + "'");
                    }
                }

                DataTable dt = DBMethods.WritePunches(la_office, punc_data);
                bool IsSuccessResult = dt.Rows[0]["ErrMess"].ToString() == "None";

                if(!IsSuccessResult) log.Error($"Ocurrió el siguiente error al sincronizar las checadas: {dt.Rows[0]["ErrMess"]}");
                return IsSuccessResult;
            }

            return false;
        }

        /// <summary>
        /// Valida que una fecha string tenga un formato correcto incluyendo la hora, minutos y segundos, y que además detecte caracteres nulos.
        /// </summary>
        /// <param name="dateTimeString"></param>
        /// <returns></returns>
        public static bool IsValidDateTime(string dateTimeString)
        {
            // Verificar si la cadena es nula o vacía
            if (string.IsNullOrWhiteSpace(dateTimeString))
            {
                return false;
            }

            // Verificar si contiene caracteres nulos
            if (dateTimeString.Contains('\0'))
            {
                return false;
            }

            // Patrón para validar formato YYYY/MM/DD HH:MM:SS
            string pattern = @"^\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}$";
            if (!Regex.IsMatch(dateTimeString, pattern))
            {
                return false;
            }

            // Intentar convertir la cadena a DateTime para validar que sea una fecha y hora válida
            try
            {
                DateTime.ParseExact(dateTimeString, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException) {
                return false;
            }
            catch (Exception) {
                return false;
            }
        }

        public static async Task<bool> SendPunchesAsync(ScantRequest la_req)
        {
            string[] requests = la_req.Question.Split('.');

            if (int.TryParse(requests[0], out int la_office))
            {
                string las_punches = UnZipString(requests[1]);

                DataTable punc_data = new()
                {
                    Columns = { "emp_id", "evt_id", "punc_time", "punc_calc" }
                };

                foreach (string str in las_punches.Split(GlobalVars.SeparAtor))
                {
                    string[] pdata = str.Split('|');
                    string calc_time = (pdata.Length == 4) ? pdata[3] : "null";

                    punc_data.Rows.Add(int.Parse(pdata[0]), int.Parse(pdata[1]), pdata[2], calc_time);
                }

                DataTable dt = await DBMethods.WritePunchesAsync(la_office, punc_data);

                return (dt.Rows[0]["ErrMess"].ToString() == "None");
            }

            return false;
        }
        #endregion

        #region Métodos para manejo de los parámetros del sistema
        /// <summary>
        /// Recupera los parámetros del sistema y los almacena en la colección correspondiente.
        /// </summary>
        /// <param name="search_offline">Indica si deben recuperarse localmente los parámetros en modo sin conexión.</param>
        public static bool RetrieveParams()
        {
            try {
                if (GlobalVars.BeOffline) {
                    GlobalVars.sysParams = UnpackParams(RegAccess.GetSysParams());

                    return GlobalVars.sysParams != null && GlobalVars.sysParams.Count >= 1;
                } else {
                    GlobalVars.sysParams = DtToList<ParamData>(ListToDt([.. SysParams().Cast<object>()]));
                    return RegAccess.SaveSysParams(PackParams(GlobalVars.sysParams));
                }
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Convierte una lista de parámetros del sistema en una cadena de texto para su almacenamiento en caché.
        /// </summary>
        /// <param name="los_params">La lista de parámetros que se convertirá.</param>
        /// <returns>Cadena de texto que contiene la lista de parámetros.</returns>
        public static string PackParams(List<ParamData>? los_params)
        {
            int el_idx = 0;
            string[] param_strs = new string[los_params.Count];

            foreach (ParamData param in los_params)
            {
                string the_param = param.number.ToString() + '|' + param.value;
                param_strs[el_idx] = the_param;
                el_idx++;
            }

            return string.Join("§", param_strs);
        }

        /// <summary>
        /// Convierte una cadena de texto en una lista de parámetros del sistema.
        /// </summary>
        /// <param name="params_str">Cadena de texto que contiene la lista de parámetros.</param>
        /// <returns>Lista de parámetros.</returns>
        public static List<ParamData> UnpackParams(string? params_str)
        {
            List<ParamData> la_resp = [];
            string[] param_strs = params_str.Split(['§']);

            foreach (string str in param_strs)
            {
                string[] param_vals = str.Split(['|']);
                _ = int.TryParse(param_vals[0], out int el_num);

                if (el_num > 0)
                {
                    la_resp.Add(
                        new ParamData {
                            number = int.Parse(param_vals[0]),
                            value = param_vals[1]
                        }
                    );
                }
            }

            return la_resp;
        }

        /// <summary>
        /// Establece o retira el modo de trabajo "Fuera de línea" para la aplicación del reloj.
        /// </summary>
        public static void SetOfflineMode()
        {
            if (GlobalVars.VTAttModule == 1) {
                GlobalVars.BeOffline = GlobalVars.OfflineInvoked || !CommonValids.ValidInternetConn();
            } else {
                GlobalVars.BeOffline = !CommonValids.ValidInternetConn();
            }
        }

        /// <summary>
        /// Regresa el valor de un párametro como entero.
        /// </summary>
        /// <param name="el_param">El ID del parámetro que se buscará.</param>
        /// <returns>El valor del parámetro solicitado.</returns>
        public static int ParamInt(int el_param)
        {
            _ = int.TryParse(ParamStr(el_param), out int la_resp);

            return la_resp;
        }

        /// <summary>
        /// Recupera el valor de un parámetro del sistema.
        /// </summary>
        /// <param name="el_param">El ID del parámetro que se buscará.</param>
        /// <returns>El valor del parámetro solicitado.</returns>
        public static string? ParamStr(int el_param)
        {
            string? str = null;
            try {
                if(GlobalVars.sysParams.Count > 0) str = GlobalVars.sysParams?.ElementAt(GlobalVars.sysParams.FindIndex(p => p.number == el_param)).value;
            } catch {}

            return str;
        }

        /// <summary>
        /// Regresa el valor de un parámetro como fecha y hora.
        /// </summary>
        /// <param name="el_param">El ID del parámetro que se buscará.</param>
        /// <returns>El valor del parámetro solicitado.</returns>
        public static DateTime ParamDTime(int el_param)
        {
            _ = DateTime.TryParse(ParamStr(el_param), out DateTime la_resp);

            return la_resp;
        }

        /// <summary>
        /// Regresa el valor de un parámetro como un intervalo de tiempo.
        /// </summary>
        /// <param name="el_param">El ID del parámetro que se buscará.</param>
        /// <returns>El valor del parámetro solicitado.</returns>
        public static TimeSpan ParamTSpan(int el_param)
        {
            _ = TimeSpan.TryParse(ParamStr(el_param), out TimeSpan la_resp);

            return la_resp;
        }
        #endregion

        #region Métodos para manejo de imágenes
        /// <summary>
        /// Convierte una cadena de texto Base64 a una imagen Bitmap de Avalonia compatible para view/viewmodel
        /// </summary>
        /// <param name="notimg">Cadena de imagen en Base64</param>
        /// <returns></returns>
        public static Bitmap? Base64ToBitmap(string notimg)
        {
            try {
                byte[] imageBytes = Convert.FromBase64String(notimg);
                using MemoryStream stream = new(imageBytes);
                return new Bitmap(stream);
            } catch {
                return null;
            }
        }

        #endregion

        #region Métodos para recuperar la configuración del sistema
                /// <summary>
        /// Desencripta información de un objeto.
        /// </summary>
        /// <param name="EncryptedData">Cadena encriptada.</param>
        /// <returns>Array con las propiedades del objeto.</returns>
        public static string[] UnpackCryptString(string EncryptedData)
        {
            try
            {
                SimpleAES aes_crypt = new();
                string[] la_resp = (aes_crypt.DecryptString(EncryptedData)).Split(GlobalVars.SeparAtor);

                return la_resp;
            } catch {
                return [];
            }
        }

        /// <summary>
        /// Crea el objeto de información de conexión a la base de datos a partir de un archivo encriptado con extensión .hash.
        /// </summary>
        /// <returns></returns>
        public static async Task<ConnDetails> GetDBConn(string filePath)
        {
            ConnDetails la_resp = new();
            try
            {
                using var streamReader = new StreamReader(filePath);

                // Reads all the content of file as a text.
                string fileContent = await streamReader.ReadToEndAsync();
                string[] the_keys = UnpackCryptString(fileContent);

                la_resp.server = the_keys[0];
                la_resp.db = the_keys[1];
                la_resp.user = the_keys[2];
                la_resp.passwd = the_keys[3];
            }
            catch { }

            return la_resp;
        }

        public static async Task<bool> SaveConnectionSettings(ConnDetails config)
        {
            MainSettings db_settings = new() {
                Db_server = config.server,
                Db_name = config.db,
                Db_user = config.user,
                Db_pass = config.passwd
            };

            if (DBConnection.TestConnection(db_settings) && RegAccess.SetDBConSettings(db_settings))
            {
                return await Task.FromResult(true);
            }
            else
            {
                return await Task.FromResult(false);
            }
        }

        /// <summary>
        /// Trae la colección de parámetros del sistema.
        /// </summary>
        /// <returns>Los parámetros del sistema.</returns>
        public static DataTable GetParams()
        {
            return DBMethods.GetParam(0);
        }
        #endregion

        #region Métodos para recuperación de privilegios de usuario

        /// <summary>
        /// Convierte el DataTable recibido como respuesta del servidor a una matriz con el conjunto de privilegios del usuario.
        /// </summary>
        /// <param name="priv_dt">DataTable con la relación de los privilegios.</param>
        /// <returns>Array con los privilegios.</returns>
        public static int[] ParseUsrPrivs(DataTable priv_dt)
        {
            int[] la_resp = new int[priv_dt.Rows.Count];
            int iii = 0;

            foreach (DataRow dr in priv_dt.Rows)
            {
                _ = int.TryParse(dr[0].ToString(), out int el_priv);
                la_resp[iii] = el_priv;
                iii++;
            }

            return la_resp;
        }

        /// <summary>
        /// Recupera los privilegios del usuario actual y los almacena en la variable global correspondiente.
        /// </summary>
        public static void SetUsrPrivs()
        {
            GlobalVars.UserPrivileges = null;

            try {
                GlobalVars.UserPrivileges = ParseUsrPrivs(GetUsrPrivs(new ScantRequest { Question = GlobalVars.mySession.usrname }));
            } catch {
                GlobalVars.UserPrivileges = [0];
            }
        }

        #endregion
    }
}
