using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using VTACheckClock.Services;

namespace VTACheckClock.DBAccess
{
    class DBInterface
    {
        /// <summary>
        /// Crea el objeto SqlCommand con base a los parámetros especificados.
        /// </summary>
        /// <param name="command_type">Indica si se tratará de un comando de texto o de un Procedimiento Almacenado.</param>
        /// <returns>El objeto del comando.</returns>
        public static SqlCommand CrearComando(int command_type)
        {
            string cadenaConexion = DBConnection.SQLConnect();

            SqlConnection conexion = new() {
                ConnectionString = cadenaConexion
            };

            SqlCommand comando = conexion.CreateCommand();
            comando.CommandType = (command_type == 1) ? CommandType.Text : CommandType.StoredProcedure;

            return comando;
        }

        /// <summary>
        /// Ejecuta un comando SELECT o un Procedimiento Almacenado y devuelve un DataTable.
        /// </summary>
        /// <param name="comando">Comando SQL a ejecutar.</param>
        /// <returns>Salida del comando o información de la excepción, en su caso.</returns>
        public static DataTable EjecutarComandoSelect(SqlCommand comando)
        {
            DataTable tabla = new();
            try {
                comando.Connection.Open();
            }
            catch {}

            SqlDataAdapter adaptador = new()
            {
                SelectCommand = comando
            };

            try
            {
                adaptador.Fill(tabla);
            } catch (Exception ex) {
                tabla = CommonObjs.ErrorTable(ex.Message);
            } finally {
                comando.Connection.Close();
            }

            return tabla;
        }

        public static async Task<DataTable> EjecutarComandoSelectAsync(SqlCommand comando)
        {
            DataTable tabla = new();

            await comando.Connection.OpenAsync();

            try {
                using SqlDataReader reader = await comando.ExecuteReaderAsync();
                await Task.Run(() => tabla.Load(reader));
            }
            catch (Exception ex) {
                tabla = CommonObjs.ErrorTable(ex.Message);
            } finally {
                comando.Connection.Close();
            }

            return tabla;
        }

        /// <summary>
        /// Ejecuta un comando SELECT o un Procedimiento Almacenado y devuelve un DataSet.
        /// </summary>
        /// <param name="comando">Comando SQL a ejecutar.</param>
        /// <returns>Salida del comando o información de la excepción, en su caso.</returns>
        public static DataSet EjecutarComandoDataset(SqlCommand comando)
        {
            DataSet la_data = new();

            comando.Connection.Open();
            SqlDataAdapter adaptador = new();

            try
            {
                adaptador.SelectCommand = comando;
                adaptador.Fill(la_data);
            }

            catch (Exception ex)
            {
                la_data.Tables.Add(CommonObjs.ErrorTable(ex.Message));
            }

            finally
            {
                comando.Connection.Close();
            }

            return la_data;
        }

        /// <summary>
        /// Ejecuta un comando de modificación (INSERT/UPDATE/DELETE) y devuelve un entero con el código de la excepción (0 = sin errores).
        /// </summary>
        /// <param name="comando">Comando SQL a ejecutar.</param>
        /// <returns>Código numérico de la excepción o 0 en caso de no ocurrir errores.</returns>
        public static int EjecutaComandoModif(SqlCommand comando)
        {
            int la_resp;

            try
            {
                comando.Connection.Open();
                comando.ExecuteNonQuery();

                la_resp = 0;
            }

            catch (Exception ex)
            {
                la_resp = ex.HResult;
            }

            finally
            {
                comando.Connection.Close();
            }

            return la_resp;
        }

        public static async Task<int> EjecutaComandoModifAsync(SqlCommand comando)
        {
            int la_resp;

            try {
                await comando.Connection.OpenAsync();
                await comando.ExecuteNonQueryAsync();

                la_resp = 0;
            } catch (Exception ex) {
                la_resp = ex.HResult;
            }

            finally {
                comando.Connection.Close();
            }

            return la_resp;
        }
    }
}
