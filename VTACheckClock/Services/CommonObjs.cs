using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services
{
    class CommonObjs
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        public class RemoteTime
        {
            public string? TimeString { get; set; }
            public DateTime CurrentTime { get; set; }
        }

        /// <summary>
        /// Devuelve la hora actual. Si no se puede obtener la hora de Internet, devuelve la hora local actual del sistema.
        /// </summary>
        public static async Task<RemoteTime> GetTimeNow() {
            var time = await GetDateTime();

            if (time == DateTime.MinValue) {
                time = DateTime.Now;
            }

            return new RemoteTime {
                CurrentTime = time,
                TimeString = time.ToString("HH:mm:ss")
            };
        }

        /// <summary>
        /// Sincroniza la hora de Internet y la convierte a la zona horaria configurada. En caso de no haber conexion con Internet devuelve DateTime.MinValue.
        /// </summary>
        /// <returns></returns>
        public static async Task<DateTime> GetDateTime()
        {
            DateTime dateTime = DateTime.MinValue;  //DateTime.Now;
            try {
                DateTime localutc = DateTime.UtcNow;
                string? local_tz = TimeZoneInfo.Local.Id;

                if (!GlobalVars.BeOffline) {
                    DateTime remoteutc = await GetNetworkTimeAsync();
                    DateTime dat = DateTime.SpecifyKind(remoteutc, DateTimeKind.Utc);
                    string? remote_tz = GlobalVars.TimeZone;
                    string? current_tz = string.IsNullOrEmpty(remote_tz) ? local_tz : remote_tz;
                    
                    TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(current_tz);
                    dateTime = TimeZoneInfo.ConvertTimeFromUtc(dat, timeZone);
                }
            } catch {
                dateTime = DateTime.MinValue; //DateTime.Now;
            }

            return dateTime;
        }

        /// <summary>
        /// Para obtener la zona horaria utilizando la ubicación geográfica del dispositivo desde Internet, 
        /// se puede utilizar una API de geolocalización para obtener las coordenadas geográficas del dispositivo 
        /// y luego utilizar una API de zona horaria para obtener la zona horaria correspondiente. 
        /// Hay varias API disponibles para realizar estas tareas, como por ejemplo la API de geolocalización 
        /// de IPInfo.io y la API de zona horaria de TimezoneDB.
        /// </summary>
        /// <returns>Timezone ID en una cadena de texto</returns>
        public static async Task<string> GetTimeZoneAsync()
        {
            const string GeoLocationUrl = "https://ipinfo.io/json";
            const string TimeZoneUrlFormat = "http://api.timezonedb.com/v2.1/get-time-zone?key={0}&format=json&by=position&lat={1}&lng={2}";
            
            try {
                // Obtener la ubicación geográfica del dispositivo desde Internet
                using var httpClient = new HttpClient();
                var geoLocationResponse = await httpClient.GetStringAsync(GeoLocationUrl);
                var geoLocationData = JObject.Parse(geoLocationResponse);
                var latitude = geoLocationData["loc"].ToString().Split(',')[0];
                var longitude = geoLocationData["loc"].ToString().Split(',')[1];

                // Obtener la zona horaria correspondiente utilizando la API de zona horaria
                var timeZoneUrl = string.Format(TimeZoneUrlFormat, "4PEXXQOOT0SI", latitude, longitude);
                var timeZoneResponse = await httpClient.GetStringAsync(timeZoneUrl);
                var timeZoneData = JObject.Parse(timeZoneResponse);
                return timeZoneData["zoneName"].ToString();
            } catch {
                return TimeZoneInfo.Local.Id;
            }
        }

        /// <summary>
        /// Obtiene de manera asincrónica y precisa la hora actual desde un servidor NTP (Network Time Protocol) específico, utilizando sockets UDP y manipulando datos de tiempo en formato binario.
        /// </summary>
        /// <returns></returns>
        public static async Task<DateTime> GetNetworkTimeAsync()
        {
            const string NtpServer = "time.windows.com";

            try {
                // Arreglo de bytes que almacena los datos de la solicitud NTP
                var ntpData = new byte[48];
                ntpData[0] = 0x1B;

                var endPoint = new IPEndPoint(Dns.GetHostEntry(NtpServer).AddressList[0], 123);
                // Configura un socket UDP para la comunicación con el servidor NTP
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                await socket.ConnectAsync(endPoint);
                socket.ReceiveTimeout = 3000;
                await socket.SendAsync(ntpData, SocketFlags.None);
                await socket.ReceiveAsync(ntpData, SocketFlags.None);
                socket.Close();

                // Especificar el índice del primer byte del campo de tiempo de transmisión
                const int transmitTimeOffset = 40;

                // Obtiene la hora actual como una marca de tiempo de 64 bits
                ulong intPart = BitConverter.ToUInt32(ntpData, transmitTimeOffset);
                ulong fractPart = BitConverter.ToUInt32(ntpData, transmitTimeOffset + 4);

                //Convert From big-endian to little-endian
                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                var networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

                return networkDateTime;
            } catch(Exception ex) {
                log.Warn("Error al obtener la hora de red en {0}: {1}", NtpServer, ex.Message);
                return DateTime.Now;
            }
        }

        public static async Task<DateTime> GetNetworkTimeAsyncSafe()
        {
            try {
                return await GetNetworkTimeAsync();
            } catch {
                return DateTime.Now;
            }
        }

        /// <summary>
        /// Obtiene la hora de Internet en formato UTC.
        /// </summary>
        /// <returns></returns>
        public static DateTime GetNetworkTime()
        {
            //default Windows time server
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(ipEndPoint);

            //Stops code hang if NTP is blocked
            socket.ReceiveTimeout = 3000;

            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            DateTime endDate = networkDateTime;//.ToLocalTime();
            return endDate;
        }

        /// <summary>
        /// Convertir el valor de las variables del formato big-endian al formato little-endian es necesario para obtener la hora correcta de internet
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        static uint SwapEndianness(ulong x)
        {
            return (uint)(
              ((x & 0x000000ff) << 24) +
              ((x & 0x0000ff00) << 8) +
              ((x & 0x00ff0000) >> 8) +
              ((x & 0xff000000) >> 24)
            );
        }

        /// <summary>
        /// Devuelve la fecha actual en formato largo.
        /// </summary>
        public static string TodayLongDate => (CommonProcs.UpperFirst(DateTime.Now.ToLongDateString()) + ".");

        /// <summary>
        /// Crea un DataTable con información del error en la consulta de la base de datos.
        /// </summary>
        /// <param name="err_mess">Mensaje de error de la excepción.</param>
        /// <returns>DataTable con información del error.</returns>
        public static DataTable ErrorTable(string err_mess)
        {
            DataTable dt = new();

            dt.Columns.Add("ERROR");

            DataRow dr = dt.NewRow();

            dr[0] = err_mess;
            dt.Rows.Add(dr);

            return dt;
        }

        /// <summary>
        /// Matriz de tipos de evento de asistencia.
        /// </summary>
        public static string[] EvTypes
        {
            get {
                return ["Desconocido", "Entrada", "Salida"];
            }
        }

        /// <summary>
        /// Devuelve un DataTable para almacenar registros de asistencia, vacío.
        /// </summary>
        public static DataTable VoidPunches
        {
            get
            {
                DataTable dt = new() {
                    Columns = { "EmpID", "EvID", "PuncTime", "PuncCalc" }
                };

                return dt;
            }
        }

        /// <summary>
        /// Devuelve un DataTable para almacenar huellas dactilares, vacío.
        /// </summary>
        /// <Returns>Datatable vacio con los encabezados válidos</Returns>
        public static DataTable VoidFMDs
        {
            get
            {
                DataTable dt = new() {
                    Columns = { "OffID", "EmpID", "FingerID", "FingerFMD", "EmpNum", "EmpName", "EmpPass" }
                };

                return dt;
            }
        }

        /// <summary>
        /// Devuelve un objeto con una respuesta simple para el cliente.
        /// </summary>
        /// <param name="la_resp">La respuesta a empaquetar.</param>
        /// <returns>Objeto con la respuesta empaquetada.</returns>
        public static ScantResponse BeBlunt(bool la_resp)
        {
            return new ScantResponse { ack = la_resp };
        }

        /// <summary>
        /// Genera una clave API para el acceso a un hub de SignalR.
        /// </summary>
        /// <param name="SignalRHubUrl"></param>
        /// <param name="SignalRHubName"></param>
        /// <param name="SignalRMethodName"></param>
        /// <returns></returns>
        public static async Task<string> GenerateSignalRApiKeyAsync(string SignalRHubUrl, string SignalRHubName, string SignalRMethodName)
        {
            // Pseudocódigo:
            // 1. Generar un array de bytes aleatorios usando RandomNumberGenerator.
            // 2. Convertir los bytes a una cadena segura (por ejemplo, Base64Url).
            // 3. Devolver la cadena como clave API.

            return await Task.Run(() =>
            {
                const int keyLength = 32; // 256 bits
                byte[] keyBytes = new byte[keyLength];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(keyBytes);
                }

                // Usar Base64Url para evitar caracteres no seguros en URLs
                string apiKey = Convert.ToBase64String(keyBytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');

                return apiKey;
            });
        }
    }
}
