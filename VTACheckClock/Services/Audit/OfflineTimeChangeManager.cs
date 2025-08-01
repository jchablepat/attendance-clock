using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using VTACheckClock.DBAccess;

public class OfflineTimeChangeManager : IDisposable
{
    private readonly string _localDbPath;
    private static readonly Logger _log = LogManager.GetLogger("app_logger");
    private readonly Timer _syncTimer;
    private readonly SemaphoreSlim _syncSemaphore;
    private bool _isOnline = false;

    public OfflineTimeChangeManager()
    {
        _syncSemaphore = new SemaphoreSlim(1, 1);

        // Base de datos local SQLite (.sqlite o .db)
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeChangeMonitor");
        Directory.CreateDirectory(appDataPath);
        _localDbPath = Path.Combine(appDataPath, "timechanges_offline.db");

        InitializeLocalDatabase();

        // Monitor de conexión cada 30 segundos
        _syncTimer = new Timer(CheckConnectionAndSync!, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private void InitializeLocalDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        connection.Open();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS TimeChangeLog_Offline (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogData TEXT NOT NULL,           -- JSON con toda la información
                CreatedAt TEXT NOT NULL,         -- Timestamp local
                AttemptCount INTEGER DEFAULT 0,  -- Intentos de sincronización
                LastAttempt TEXT NULL,           -- Último intento de sync
                ErrorMessage TEXT NULL,          -- Último error
                IsSynced INTEGER DEFAULT 0,      -- 0 = No sincronizado, 1 = Sincronizado
                SyncedAt TEXT NULL               -- Cuando se sincronizó
            );

            CREATE INDEX IF NOT EXISTS idx_offline_synced ON TimeChangeLog_Offline(IsSynced);
            CREATE INDEX IF NOT EXISTS idx_offline_created ON TimeChangeLog_Offline(CreatedAt);
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        command.ExecuteNonQuery();
    }

    public async Task SaveTimeChangeOfflineAsync(TimeChangeInfo timeChangeInfo)
    {
        try
        {
            // Primero intentar guardar en remoto si hay conexión
            if (await IsOnlineAsync())
            {
                try
                {
                    await SaveToRemoteDatabase(timeChangeInfo);
                    _log.Info("Cambio de hora guardado directamente en base remota");
                    return;
                }
                catch (Exception ex)
                {
                    _log.Warn($"Error guardando en remoto, usando almacén local: {ex.Message}");
                }
            }

            // Guardar localmente
            await SaveToLocalDatabase(timeChangeInfo);
            _log.Info("Cambio de hora guardado en almacén local para sincronización posterior");
        }
        catch (Exception ex)
        {
            _log.Error($"Error crítico guardando cambio de hora: {ex.Message}");
        }
    }

    private async Task SaveToLocalDatabase(TimeChangeInfo timeChangeInfo)
    {
        var jsonData = JsonConvert.SerializeObject(timeChangeInfo, Formatting.None);

        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO TimeChangeLog_Offline (LogData, CreatedAt) VALUES (@LogData, @CreatedAt)";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@LogData", jsonData);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("O")); // ISO 8601

        await command.ExecuteNonQueryAsync();
    }

    private async void CheckConnectionAndSync(object state)
    {
        try
        {
            var currentlyOnline = await IsOnlineAsync();

            // Si acabamos de conectarnos, sincronizar
            if (currentlyOnline && !_isOnline)
            {
                _log.Info("Conexión a Internet detectada, iniciando sincronización...");
                _ = Task.Run(SyncPendingChangesAsync); // Async sin await para no bloquear el timer
            }

            _isOnline = currentlyOnline;
        }
        catch (Exception ex)
        {
            _log.Error($"Error en CheckConnectionAndSync: {ex.Message}");
        }
    }

    private static async Task<bool> IsOnlineAsync()
    {
        try
        {
            // Verificar conectividad de red básica
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            // Intentar conexión a la base de datos remota
            using var connection = new SqlConnection(DBConnection.SQLConnect());
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await connection.OpenAsync(cancellationToken.Token);

            // Verificar que podemos hacer una consulta simple
            using var command = new SqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken.Token);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SyncPendingChangesAsync()
    {
        if (!await _syncSemaphore.WaitAsync(1000)) // Evitar múltiples sincronizaciones simultáneas
        {
            _log.Warn("Sincronización ya en progreso, omitiendo...");
            return;
        }

        try
        {
            var pendingChanges = await GetPendingChangesAsync();

            if (pendingChanges.Count == 0)
            {
                _log.Info("No hay cambios pendientes de sincronización");
                return;
            }

            _log.Info($"Sincronizando {pendingChanges.Count} cambios pendientes...");

            int successCount = 0;
            int errorCount = 0;

            foreach (var change in pendingChanges)
            {
                try
                {
                    var timeChangeInfo = JsonConvert.DeserializeObject<TimeChangeInfo>(change.LogData);

                    // Agregar metadatos de sincronización
                    timeChangeInfo.SyncMetadata = new SyncMetadata
                    {
                        OriginalTimestamp = DateTime.Parse(change.CreatedAt),
                        SyncTimestamp = DateTime.Now,
                        AttemptCount = change.AttemptCount + 1,
                        WasOffline = true
                    };

                    await SaveToRemoteDatabase(timeChangeInfo);
                    await MarkAsSynced(change.Id);

                    successCount++;
                    _log.Debug($"Sincronizado cambio ID {change.Id}");
                }
                catch (Exception ex)
                {
                    await UpdateSyncAttempt(change.Id, ex.Message);
                    errorCount++;
                    _log.Error($"Error sincronizando cambio ID {change.Id}: {ex.Message}");
                }
            }

            _log.Info($"Sincronización completada: {successCount} exitosos, {errorCount} errores");

            // Limpiar registros antiguos ya sincronizados (más de 30 días)
            await CleanupOldSyncedRecords();
        }
        finally
        {
            _syncSemaphore.Release();
        }
    }

    private async Task<List<OfflineLogEntry>> GetPendingChangesAsync()
    {
        var pendingChanges = new List<OfflineLogEntry>();

        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        // Obtener registros no sincronizados, limitando reintentos fallidos
        var sql = @"
            SELECT Id, LogData, CreatedAt, AttemptCount, LastAttempt, ErrorMessage
            FROM TimeChangeLog_Offline 
            WHERE IsSynced = 0 
              AND (AttemptCount < 5 OR datetime(LastAttempt) < datetime('now', '-1 hour'))
            ORDER BY CreatedAt ASC 
            LIMIT 100"; // Procesar en lotes

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (reader.Read())
        {
            pendingChanges.Add(new OfflineLogEntry
            {
                Id = reader.GetInt64("Id"),
                LogData = reader.GetString("LogData"),
                CreatedAt = reader.GetString("CreatedAt"),
                AttemptCount = reader.GetInt32("AttemptCount"),
                LastAttempt = reader.IsDBNull("LastAttempt") ? null : reader.GetString("LastAttempt"),
                ErrorMessage = reader.IsDBNull("ErrorMessage") ? null : reader.GetString("ErrorMessage")
            });
        }

        return pendingChanges;
    }

    private async Task MarkAsSynced(long id)
    {
        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        var sql = @"
            UPDATE TimeChangeLog_Offline 
            SET IsSynced = 1, SyncedAt = @SyncedAt 
            WHERE Id = @Id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@SyncedAt", DateTime.Now.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private async Task UpdateSyncAttempt(long id, string errorMessage)
    {
        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        var sql = @"
            UPDATE TimeChangeLog_Offline 
            SET AttemptCount = AttemptCount + 1, 
                LastAttempt = @LastAttempt,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@LastAttempt", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("@ErrorMessage", errorMessage.Length > 500 ? errorMessage[..500] : errorMessage);

        await command.ExecuteNonQueryAsync();
    }

    private async Task CleanupOldSyncedRecords()
    {
        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        var sql = @"
            DELETE FROM TimeChangeLog_Offline 
            WHERE IsSynced = 1 
              AND datetime(SyncedAt) < datetime('now', '-30 days')";

        using var command = new SqliteCommand(sql, connection);
        var deletedCount = await command.ExecuteNonQueryAsync();

        if (deletedCount > 0)
        {
            _log.Info($"Limpieza completada: {deletedCount} registros antiguos eliminados");
        }
    }

    // Método actualizado para guardar en remoto con metadatos de sync
    private static async Task SaveToRemoteDatabase(TimeChangeInfo info)
    {
        SqlCommand command = DBInterface.CrearComando(2);

        command.CommandText = "[attendance].[SpInsertTimeChangeLog]";
        command.Parameters.AddWithValue("@OfficeId", info.OfficeId);
        command.Parameters.AddWithValue("@EventDateTime", info.EventDateTime);
        command.Parameters.AddWithValue("@PreviousSystemTime", info.PreviousSystemTime);
        command.Parameters.AddWithValue("@NewSystemTime", info.NewSystemTime);
        command.Parameters.AddWithValue("@TimeDifferenceSeconds", info.TimeDifferenceSeconds);
        command.Parameters.AddWithValue("@MachineName", info.MachineName);
        command.Parameters.AddWithValue("@UserName", info.UserName);
        command.Parameters.AddWithValue("@ProcessName", info.ProcessName);
        command.Parameters.AddWithValue("@NtpServerUsed", info.NtpServerUsed);
        command.Parameters.AddWithValue("@IsNtpSynchronization", info.IsNtpSynchronization);
        command.Parameters.AddWithValue("@ChangeType", info.ChangeType);
        command.Parameters.AddWithValue("@IsSignificantChange", info.IsSignificantChange);
        command.Parameters.AddWithValue("@IsSuspicious", info.IsSuspicious);
        command.Parameters.AddWithValue("@SuspicionReason", info.SuspicionReason);
        command.Parameters.AddWithValue("@ApplicationState", info.ApplicationState);
        command.Parameters.AddWithValue("@TimeZoneId", info.TimeZoneId);
        command.Parameters.AddWithValue("@IsDaylightSavingTime", info.IsDaylightSavingTime);
        command.Parameters.AddWithValue("@NetworkConnected", info.NetworkConnected);
        command.Parameters.AddWithValue("@NtpSyncEnabled", info.NtpSyncEnabled);
        command.Parameters.AddWithValue("@SystemUptime", info.SystemUptime);
        command.Parameters.AddWithValue("@LastBootTime", info.LastBootTime);
        command.Parameters.AddWithValue("@AdditionalData", info.AdditionalData);

        // Nuevos parámetros de sincronización
        command.Parameters.AddWithValue("@OriginalTimestamp", info.SyncMetadata?.OriginalTimestamp ?? info.EventDateTime);
        command.Parameters.AddWithValue("@SyncTimestamp", info.SyncMetadata?.SyncTimestamp ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@WasOfflineSync", info.SyncMetadata?.WasOffline ?? false);
        command.Parameters.AddWithValue("@SyncAttemptCount", info.SyncMetadata?.AttemptCount ?? 1);

        await DBInterface.EjecutaComandoModifAsync(command);
    }

    // Método para forzar sincronización manual (útil para pruebas o administración)
    public async Task<SyncResult> ForceSyncAsync()
    {
        _log.Info("Sincronización manual iniciada...");

        var pendingCount = await GetPendingChangesCountAsync();
        await SyncPendingChangesAsync();
        var remainingCount = await GetPendingChangesCountAsync();

        return new SyncResult
        {
            InitialPendingCount = pendingCount,
            RemainingPendingCount = remainingCount,
            SyncedCount = pendingCount - remainingCount,
            WasSuccessful = remainingCount < pendingCount
        };
    }

    private async Task<int> GetPendingChangesCountAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_localDbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT COUNT(*) FROM TimeChangeLog_Offline WHERE IsSynced = 0", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _syncSemaphore?.Dispose();
    }
}

// Clases de apoyo adicionales
public class OfflineLogEntry
{
    public long Id { get; set; }
    public string LogData { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string? LastAttempt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncMetadata
{
    public DateTime OriginalTimestamp { get; set; }
    public DateTime SyncTimestamp { get; set; }
    public int AttemptCount { get; set; }
    public bool WasOffline { get; set; }
}

public class SyncResult
{
    public int InitialPendingCount { get; set; }
    public int RemainingPendingCount { get; set; }
    public int SyncedCount { get; set; }
    public bool WasSuccessful { get; set; }
}

// Actualizar la clase TimeChangeInfo para incluir metadatos de sync
public partial class TimeChangeInfo
{
    public int OfficeId { get; set; }
    public DateTime EventDateTime { get; set; }
    public DateTime? PreviousSystemTime { get; set; }
    public DateTime NewSystemTime { get; set; }
    public long? TimeDifferenceSeconds { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string NtpServerUsed { get; set; } = string.Empty;
    public bool IsNtpSynchronization { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public bool IsSignificantChange { get; set; }
    public bool IsSuspicious { get; set; }
    public string SuspicionReason { get; set; } = string.Empty;
    public string ApplicationState { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public bool? IsDaylightSavingTime { get; set; }
    public bool? NetworkConnected { get; set; }
    public bool? NtpSyncEnabled { get; set; }
    public long? SystemUptime { get; set; }
    public DateTime? LastBootTime { get; set; }
    public string AdditionalData { get; set; } = string.Empty;
    public SyncMetadata? SyncMetadata { get; set; }
}