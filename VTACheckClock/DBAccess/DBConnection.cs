using System.Data;
using Microsoft.Data.SqlClient;
using VTACheckClock.Models;
using VTACheckClock.Services;

namespace VTACheckClock.DBAccess
{
    class DBConnection
    {
        /// <summary>
        /// Crea la línea de conexión a la base de datos de SQL Server.
        /// </summary>
        /// <returns>Línea de conexión.</returns>
        public static string SQLConnect() {
            MainSettings db_settings = RegAccess.GetMainSettings() ?? new();
            string la_resp = $"Data Source={db_settings.Db_server};Initial Catalog={db_settings.Db_name};User Id={db_settings.Db_user}; Password={db_settings.Db_pass};Connection Timeout=15;TrustServerCertificate=Yes;";
            return la_resp;
        }

        public static bool ValidDBConnection(string db_server, string db_name, string db_user, string db_pass)
        {
            SqlConnection connection = new(@"Data Source=" + db_server + ";Initial Catalog=" + db_name + ";User ID=" + db_user + ";Password=" + db_pass + ";TrustServerCertificate=Yes;");

            try {
                connection.Open();
                return connection.State == ConnectionState.Open;
            } catch {
                return false;
            }
            finally {
                connection.Close();
            }
        }

        public static bool TestConnection(MainSettings db_settings)
        {
            SqlConnection connection = new(@"Data Source=" + db_settings.Db_server + ";Initial Catalog="+ db_settings.Db_name +";User ID="+ db_settings.Db_user +";Password="+ db_settings.Db_pass + ";TrustServerCertificate=Yes");
        
            try {
                connection.Open();
                return connection.State == ConnectionState.Open;
            } catch {
                return false;
            } finally {
                connection.Close();
            }
        }
    }
}
