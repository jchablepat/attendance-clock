using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services;

namespace VTACheckClock.DBAccess
{
    class DBMethods
    {
        #region Configuración del sistema

        /// <summary>
        /// Función simple para obtener la hora del servidor (motor de base de datos).
        /// </summary>
        /// <returns>La fecha y hora del servidor en un registro de un DataTable.</returns>
        public static DataTable GetServTime()
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT GETDATE() AS 'FecHora';";

            return DBInterface.EjecutarComandoSelect(comando);
        }

        /// <summary>
        /// Obtiene la información de los parámetros del sistema.
        /// </summary>
        /// <param name="param_num">Número del parámetro (opcional, 0 para todos).</param>
        /// <returns>Información de los parámetros (número/valor).</returns>
        public static DataTable GetParam(int param_num = 0)
        {
            int? el_parameter = null;
            if (param_num > 0) el_parameter = param_num;

            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT params.[ParamNum], params.[ParamValue] FROM [attendance].[ParamData] params WHERE (@el_param IS NULL OR params.[ParamNum] = @el_param) ORDER BY params.[ParamNum] ASC;";
            comando.Parameters.AddWithValue("@el_param", el_parameter as object ?? DBNull.Value);

            return DBInterface.EjecutarComandoSelect(comando);
        }
        #endregion

        #region Empleados, oficinas y huellas
        /// <summary>
        /// Obtiene la información de las oficinas registradas en el sistema.
        /// </summary>
        /// <param name="office_id">ID de la oficina (opcional, 0 para todas).</param>
        /// <returns>Información de la(s) oficina(s) seleccionada(s).</returns>
        public static DataTable GetOffice(int office_id = 0)
        {
            int? la_office = null;
            if (office_id > 0) la_office = office_id;

            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT offices.[OffID], offices.[OffName], offices.[OffDesc], offices.[NatName] FROM [attendance].[OfficeData] offices WHERE (@la_off IS NULL OR offices.[OffID] = @la_off) ORDER BY offices.[OffID] ASC;";
            comando.Parameters.AddWithValue("@la_off", la_office as object ?? DBNull.Value);

            return DBInterface.EjecutarComandoSelect(comando);
        }
        #endregion

        #region Avisos
        /// <summary>
        /// Recupera la información de los avisos para mostrarla en la interfaz del Reloj Checador.
        /// </summary>
        /// <param name="office_id">ID de la oficina donde se mostrarán los avisos.</param>
        /// <returns>DataTable con la información de los avisos para la oficina solicitante.</returns>
        public static DataTable GetNoticesInfo(int office_id)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT notinf.[NotID], notinf.[NotTit], notinf.[NotMsg], notinf.[NotImg] FROM [attendance].[NoticeInfo] notinf WHERE (notinf.[NotSince] <= FORMAT(GETDATE(), 'yyyy/MM/dd')) AND (notinf.[NotThru] >= FORMAT(GETDATE(), 'yyyy/MM/dd')) AND notinf.[NotOffice] = @off_id ORDER BY notinf.[NotOffice] ASC;";
            comando.Parameters.AddWithValue("@off_id", office_id);

            return DBInterface.EjecutarComandoSelect(comando);
        }

        public static async Task<DataTable> GetNoticesInfoAsync(int office_id)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT notinf.[NotID], notinf.[NotTit], notinf.[NotMsg], notinf.[NotImg] FROM [attendance].[NoticeInfo] notinf WHERE (notinf.[NotSince] <= FORMAT(GETDATE(), 'yyyy/MM/dd')) AND (notinf.[NotThru] >= FORMAT(GETDATE(), 'yyyy/MM/dd')) AND notinf.[NotOffice] = @off_id ORDER BY notinf.[NotOffice] ASC;";
            comando.Parameters.AddWithValue("@off_id", office_id);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }
        #endregion

        #region Sesiones, usuarios y seguridad

        /// <summary>
        /// Obtiene los registros pertenecientes a las credenciales de inicio de sesión proporcionadas.
        /// </summary>
        /// <param name="la_session">Objeto con las credenciales de inicio de sesión.</param>
        /// <returns>Los registros coincidentes.</returns>
        public static DataTable IdentifyAccount(SessionData la_session)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT usrp.[UsrName], usrp.[UsrPass], usrp.[PrivID] FROM [attendance].[UsrPrivs] usrp WHERE usrp.[UsrName] = @el_user AND usrp.[UsrPass] = HASHBYTES('SHA2_512', CONVERT(NVARCHAR(MAX), @el_pass)) AND usrp.[PrivID] = @el_priv;";
            comando.Parameters.AddWithValue("@el_user", la_session.usrname);
            comando.Parameters.AddWithValue("@el_pass", la_session.passwd);
            comando.Parameters.AddWithValue("@el_priv", la_session.accpriv);

            return DBInterface.EjecutarComandoSelect(comando);
        }

        /// <summary>
        /// Obtiene los privilegios correspondientes al usuario solicitado.
        /// </summary>
        /// <param name="usrname"></param>
        /// <returns>DataTable con los privilegios del usuario.</returns>
        public static DataTable CheckUrPrivilege(string? usrname)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT usrp.[PrivID] FROM [attendance].[UsrPrivs] usrp WHERE usrp.[UsrName] = @el_user ORDER BY usrp.[PrivID] ASC;";
            comando.Parameters.AddWithValue("@el_user", usrname);

            return DBInterface.EjecutarComandoSelect(comando);
        }
        #endregion

        #region Registros de asistencia
        /// <summary>
        /// Recupera los últimos registros de asistencia de cada empleado para la oficina solicitante.
        /// </summary>
        /// <param name="off_id">ID de la oficina solicitante.</param>
        /// <returns>DataTable con la información de los registros de asistencia.</returns>
        public static DataTable RetrieveLastPunches(int off_id, int user_id = 0)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[GetLastPunches]";
            comando.Parameters.AddWithValue("@off_id", off_id);
            comando.Parameters.AddWithValue("@user_id", user_id);

            return DBInterface.EjecutarComandoSelect(comando);
        }

        public static async Task<DataTable> RetrieveLastPunchesAsync(int off_id, int user_id = 0)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[GetLastPunches]";
            comando.Parameters.AddWithValue("@off_id", off_id);
            comando.Parameters.AddWithValue("@user_id", user_id);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        /// <summary>
        /// Envía los registros de asistencia al Stored Procedure encargado de integrarlos a la base de datos.
        /// </summary>
        /// <param name="off_id">ID de la oficina que envía.</param>
        /// <param name="punch_data">DataTable con los registros de asistencia que se integrarán.</param>
        /// <returns>Salida del SP en formato DataTable.</returns>
        public static DataTable WritePunches(int off_id, DataTable punch_data)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[PunchRegister]";
            comando.Parameters.AddWithValue("@off_id", off_id);
            comando.Parameters.AddWithValue("@punc_data", punch_data);

            return DBInterface.EjecutarComandoSelect(comando);
        }

        public static async Task<DataTable> WritePunchesAsync(int off_id, DataTable punch_data)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[PunchRegister]";
            comando.Parameters.AddWithValue("@off_id", off_id);
            comando.Parameters.AddWithValue("@punc_data", punch_data);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        public static async Task<DataTable> PunchRegisterAsync(int off_id, int emp_id, int evt_id, string punc_time, string punc_calc)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[PunchRegisterByEmployee]";
            comando.Parameters.AddWithValue("@off_id", off_id);
            comando.Parameters.AddWithValue("@emp_id", emp_id);
            comando.Parameters.AddWithValue("@evt_id", evt_id);
            comando.Parameters.AddWithValue("@punc_time", punc_time);
            comando.Parameters.AddWithValue("@punc_calc", punc_calc);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        /// <summary>
        /// Gets the list of employees who did not punch in or out on the current date. 
        /// It may be that the entry has not been made or is duplicated.
        /// </summary>
        /// <param name="office_id"></param>
        /// <returns></returns>
        public static async Task<DataTable> GetEmployeesWithNoCheckInOut(int office_id)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[GetEmployeesWithNoCheckInOut]";
            comando.Parameters.AddWithValue("@off_id", office_id);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        public static async Task<DataTable> GetAttendanceByDateRange(int office_id, DateTime startDate, DateTime endDate)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[GetAttendanceByDateRange]";
            comando.Parameters.AddWithValue("@off_id", office_id);
            comando.Parameters.AddWithValue("@Start_Date", startDate);
            comando.Parameters.AddWithValue("@End_Date", endDate);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        public static async Task<DataTable> SaveAttendanceTime(int employeeId, int officeId, int eventId, DateTime dateTime)
        {
            var command = DBInterface.CrearComando(2);
            command.CommandText = "[attendance].[SaveAttendanceTime]";

            command.Parameters.AddWithValue("@employee_id", employeeId);
            command.Parameters.AddWithValue("@office_id", officeId);
            command.Parameters.AddWithValue("@event_id", eventId);
            command.Parameters.AddWithValue("@dateTime", dateTime);

            return await DBInterface.EjecutarComandoSelectAsync(command);
        }

        #endregion

        #region Bitácoras

        /// <summary>
        /// Guarda la marca de tiempo del cliente y el servidor, junto con información de identificación del cliente, en la tabla de bitácora correspondiente.
        /// </summary>
        /// <param name="cli_uuid">UUID del cliente.</param>
        /// <param name="off_id">ID de la oficina del cliente.</param>
        /// <param name="cli_timestamp">Marca de tiempo del cliente.</param>
        /// <returns>0 si la operación se ejecutó correctamente, de lo contrario, el código de error obtenido de la excepción.</returns>
        public static int RegisterClientTime(string cli_uuid, int off_id, DateTime cli_timestamp)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "INSERT INTO [attendance].[timestamps_log] ([client_uuid], [office_id], [client_datetime], [server_datetime]) VALUES (@cl_uuid, @cl_off, @cl_dt, GETDATE());";
            comando.Parameters.AddWithValue("@cl_uuid", cli_uuid);
            comando.Parameters.AddWithValue("@cl_off", off_id);
            comando.Parameters.AddWithValue("@cl_dt", cli_timestamp);

            return DBInterface.EjecutaComandoModif(comando);
        }

        public static async Task<int> RegisterClientTimeAsync(string cli_uuid, int off_id, DateTime cli_timestamp)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "INSERT INTO [attendance].[timestamps_log] ([client_uuid], [office_id], [client_datetime], [server_datetime]) VALUES (@cl_uuid, @cl_off, @cl_dt, GETDATE());";
            comando.Parameters.AddWithValue("@cl_uuid", cli_uuid);
            comando.Parameters.AddWithValue("@cl_off", off_id);
            comando.Parameters.AddWithValue("@cl_dt", cli_timestamp);

            return await DBInterface.EjecutaComandoModifAsync(comando);
        }

        /// <summary>
        /// Obtiene la colección de huellas dactilares (FMD's) correspondientes a la oficina seleccionada.
        /// </summary>
        /// <param name="office_id">ID de la oficina.</param>
        /// <returns>Colección de FMD's con información del empleado.</returns>
        public static DataTable GetFMDCollection(int office_id)
        {
            SqlCommand comando = DBInterface.CrearComando(1);
            comando.CommandText = "SELECT offfmd.[OffID], offfmd.[EmpID], offfmd.[FingerID], offfmd.[FingerFMD], offfmd.[EmpNum], offfmd.[EmpName], offfmd.[EmpPass] FROM [attendance].[OfficeFMDs] offfmd WHERE offfmd.[OffID] = @off_id ORDER BY offfmd.[EmpID] ASC;";
            comando.Parameters.AddWithValue("@off_id", office_id);

            return DBInterface.EjecutarComandoSelect(comando);
        }

        public static async Task<DataTable> GetFMDCollectionAsync(int office_id)
        {
            SqlCommand comando = DBInterface.CrearComando(2);
            comando.CommandText = "[attendance].[GetEmpFMD]";
            comando.Parameters.AddWithValue("@off_id", office_id);

            return await DBInterface.EjecutarComandoSelectAsync(comando);
        }

        #endregion
    }
}
