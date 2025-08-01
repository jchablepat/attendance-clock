using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using VTACheckClock.Services.Libs;
using System.Diagnostics.Eventing.Reader;

public class TimeChangeAuditService : IDisposable
{
    private readonly OfflineTimeChangeManager _offlineManager;
    private static readonly Logger _log = LogManager.GetLogger("app_logger");
    private DateTime _lastRecordedTime;
    private readonly object _lockObject = new();
    private bool _isInitialized = false;
    // Información de contexto de la aplicación
    public static string CurrentApplicationState { get; set; } = "IDLE";

    public TimeChangeAuditService()
    {
        _lastRecordedTime = DateTime.Now;

        // Inicializar el manager offline
        _offlineManager = new OfflineTimeChangeManager();
    }

    public void Initialize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            SystemEvents.TimeChanged += OnTimeChanged!;
            _isInitialized = true;
            _lastRecordedTime = DateTime.Now;

            // También monitorear cambios en zona horaria
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && e.Category == UserPreferenceCategory.Locale)
        {
            var currentTime = DateTime.Now;
            var previousTime = _lastRecordedTime;

            var eventArgs = new TimeChangeEventArgs {
                EventType = "TIMEZONE_CHANGED",
                NewTime = currentTime,
                PreviousTime = previousTime
            };

            // Actualizar tiempo conocido
            _lastRecordedTime = currentTime;

            // Registrar de forma asíncrona para no bloquear
            _ = Task.Run(() => LogTimeChangeAsync(eventArgs));
            
            Debug.WriteLine($"Cambio de zona horaria o configuración regional detectado: {e.Category}");
        }
    }

    private void OnTimeChanged(object sender, EventArgs e)
    {
        try
        {
            lock (_lockObject)
            {
                var currentTime = DateTime.Now;
                var previousTime = _lastRecordedTime;

                var eventArgs = new TimeChangeEventArgs {
                    EventType = "TIME_CHANGED",
                    NewTime = currentTime,
                    PreviousTime = previousTime
                };

                // Actualizar tiempo conocido
                _lastRecordedTime = currentTime;

                // Registrar de forma asíncrona para no bloquear
                _ = Task.Run(() => LogTimeChangeAsync(eventArgs));
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Error en OnTimeChanged: {ex.Message}");
        }
    }

    private async Task LogTimeChangeAsync(TimeChangeEventArgs args)
    {
        try
        {
            var timeChangeInfo = await GatherTimeChangeInfoAsync(args);
            // Usar el sistema offline en lugar de SaveToDatabase directo
            await _offlineManager.SaveTimeChangeOfflineAsync(timeChangeInfo);

            // Analizar si es sospechoso
            if (timeChangeInfo.IsSuspicious) {
                _log.Warn($"Cambio de hora sospechoso detectado: {timeChangeInfo.SuspicionReason}");
            }
        }
        catch (Exception ex) {
            _log.Error($"Error registrando cambio de hora: {ex.Message}");
        }
    }

    private static async Task<TimeChangeInfo> GatherTimeChangeInfoAsync(TimeChangeEventArgs args)
    {
        var info = new TimeChangeInfo {
            OfficeId = GlobalVars.this_office?.Offid ?? 0,
            EventDateTime = DateTime.Now,
            PreviousSystemTime = args.PreviousTime,
            NewSystemTime = args.NewTime,
            MachineName = Environment.MachineName,
            UserName = GetCurrentUser(),
            ApplicationState = CurrentApplicationState,
            TimeZoneId = TimeZoneInfo.Local.Id,
            IsDaylightSavingTime = TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now),
            NetworkConnected = await IsNetworkConnectedAsync(),
            NtpSyncEnabled = IsNtpSyncEnabled(),
            SystemUptime = GetSystemUptime(),
            LastBootTime = GetLastBootTime()
        };

        // Calcular diferencia de tiempo
        if (args.PreviousTime.HasValue)
        {
            var timeDiff = (args.NewTime - args.PreviousTime.Value).TotalSeconds;
            info.TimeDifferenceSeconds = (long)timeDiff;
            info.IsSignificantChange = Math.Abs(timeDiff) > 30;
        }

        await EnrichAuditRecordAsync(info);

        // Determinar tipo de cambio
        info.ChangeType = DetermineChangeType(info);

        // Evaluar si es sospechoso
        info.IsSuspicious = EvaluateIfSuspicious(info);
        if (info.IsSuspicious)
        {
            AnalyzeSuspiciousActivity(info);
        }

        return info;
    }

    private static bool EvaluateIfSuspicious(TimeChangeInfo info)
    {
        // Cambio manual durante proceso de autenticación
        if (info.ChangeType == "MANUAL")
            return true;

        // Cambios grandes hacia atrás (más de 1 minuto)
        if (info.TimeDifferenceSeconds < -60)
            return true;

        // Cambios muy grandes (más de 1 hora)
        if (Math.Abs(info.TimeDifferenceSeconds ?? 0) > 3600)
            return true;

        return false;
    }

    private static void AnalyzeSuspiciousActivity(TimeChangeInfo record)
    {
        var suspiciousReasons = new List<string>();

        // Cambio manual significativo
        if (Math.Abs((decimal)record.TimeDifferenceSeconds!) > 300) // > 5 minutos
        {
            suspiciousReasons.Add($"Cambio temporal significativo: {record.TimeDifferenceSeconds} segundos");
        }

        // Cambio hacia atrás (muy sospechoso)
        if (record.TimeDifferenceSeconds < -60) // Retroceso > 1 minuto
        {
            suspiciousReasons.Add("Cambio de hora hacia atrás");
        }

        // No es sincronización NTP
        if (!record.IsNtpSynchronization && Math.Abs((decimal)record.TimeDifferenceSeconds) > 60)
        {
            suspiciousReasons.Add("Cambio manual sin sincronización NTP");
            record.ChangeType = "MANUAL";
        }
        else if (record.IsNtpSynchronization)
        {
            record.ChangeType = "NTP_SYNC";
        }
        else if (record.SystemUptime < 300) // Sistema recién iniciado
        {
            record.ChangeType = "SYSTEM_BOOT";
        }
        else
        {
            record.ChangeType = "UNKNOWN";
        }

        // Múltiples cambios en poco tiempo
        if (record.SystemUptime > 300 && Math.Abs((decimal)record.TimeDifferenceSeconds) > 10)
        {
            suspiciousReasons.Add("Cambio en sistema estable");
        }

        record.IsSuspicious = suspiciousReasons.Count != 0;
        record.SuspicionReason = string.Join("; ", suspiciousReasons);
    }

    private static string DetermineChangeType(TimeChangeInfo info)
    {
        // Si tenemos información directa del evento de Windows, usarla
        if (info.IsNtpSynchronization)
            return "NTP_SYNC";
        
        // Si el sistema acaba de iniciar, probablemente es un ajuste de inicio
        if (info.SystemUptime < 300) // menos de 5 minutos
            return "SYSTEM_BOOT";
        
        // Verificar si es un cambio de horario de verano
        var localZone = TimeZoneInfo.Local;
        var isDstTransitionDay = IsDaylightSavingTransitionDay();
        if (isDstTransitionDay && Math.Abs(info.TimeDifferenceSeconds ?? 0) is >= 3500 and <= 3700) // ~1 hora
            return "DST_TIMEZONE_CHANGE";
        
        // Si hay conexión de red y NTP está habilitado, probablemente es automático
        if (info.NetworkConnected == true && info.NtpSyncEnabled == true)
        {
            // Cambios pequeños (< 2 minutos) con red son típicamente sincronización
            if (Math.Abs(info.TimeDifferenceSeconds ?? 0) < 120)
                return "AUTOMATIC_SYNC";
        }

        // Cambios grandes son más probablemente manuales
        if (Math.Abs(info.TimeDifferenceSeconds ?? 0) > 300) // > 5 minutos
            return "MANUAL";

        return "UNKNOWN";
    }
    private static bool IsDaylightSavingTransitionDay()
    {
        try
        {   
            var localZone = TimeZoneInfo.Local;
            if (!localZone.SupportsDaylightSavingTime)
                return false;
            
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            var tomorrow = today.AddDays(1);
            
            bool yesterdayIsDst = localZone.IsDaylightSavingTime(yesterday);
            bool todayIsDst = localZone.IsDaylightSavingTime(today);
            bool tomorrowIsDst = localZone.IsDaylightSavingTime(tomorrow);
            
            // Si hay un cambio entre ayer, hoy o mañana, estamos en un día de transición
            return (yesterdayIsDst != todayIsDst) || (todayIsDst != tomorrowIsDst);
        }
        catch
        {
            return false;
        }
    }

    private static async Task EnrichAuditRecordAsync(TimeChangeInfo record)
    {
        try
        {
            // Detectar si fue sincronización NTP
            var (IsSynchronization, ServerUsed, SyncDetails) = await CheckNtpSynchronizationAsync();
            record.IsNtpSynchronization = IsSynchronization;
            record.NtpServerUsed = ServerUsed!;

            // Obtener proceso que pudo causar el cambio
            record.ProcessName = GetRecentTimeRelatedProcess()!;

            // Información adicional en JSON
            var additionalData = new {
                OfficeName = GlobalVars.this_office?.Offname ?? "UNKNOWN",
                LocalTimeZone = TimeZoneInfo.Local.DisplayName,
                UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours,
                Environment.Is64BitOperatingSystem,
                OSVersion = GetWindowsVersion(),
                Environment.ProcessorCount
            };

            record.AdditionalData = JsonSerializer.Serialize(additionalData);
        }
        catch (Exception ex)
        {
            _log.Warn($"Error al enriquecer datos de auditoría: {ex.Message}");
        }
    }

    private static async Task<DateTime?> GetNetworkTimeAsync()
    {
        try
        {
            // Obtener hora de servidor NTP público
            var client = new UdpClient();
            client.Connect("pool.ntp.org", 123);

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // Configuración NTP

            await client.SendAsync(ntpData, 48);
            var result = await client.ReceiveAsync();

            // Parsear respuesta NTP (simplificado)
            var intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
            var fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            var networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool IsSynchronization, string? ServerUsed, string? SyncDetails)> CheckNtpSynchronizationAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 1. Verificar el registro de eventos de Windows primero
                var (changeDate, changeReason, details) = GetLastTimeChangeReason();
                bool isEventLogSync = changeReason.Contains("Sincronización automática", StringComparison.OrdinalIgnoreCase);
                // var powerShellChange = GetLastTimeChangeReasonViaPowerShell();

                // 2. Verificar servicio de tiempo de Windows
                var process = Process.Start(new ProcessStartInfo {
                    FileName = "w32tm",
                    Arguments = "/query /status /verbose",  // Añadido /verbose para más detalles
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var isSync = output.Contains("Source:") && !output.Contains("Local CMOS Clock");
                var server = ExtractNtpServerFromOutput(output);

                // 3. Verificar el tipo de sincronización configurado
                var syncType = GetNtpSyncType();
                // 4. Verificar si el servicio Windows Time está en ejecución
                bool isTimeServiceRunning = IsWindowsTimeServiceRunning();

                // Combinar la información para determinar si fue sincronización
                bool isSynchronization = isEventLogSync || (isSync && isTimeServiceRunning);

                // Detalles adicionales para diagnóstico
                string syncDetails = $"EventLog: {isEventLogSync}, W32Time: {isSync}, Service: {isTimeServiceRunning}, Type: {syncType}";

                return (isSynchronization, server, syncDetails);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Error al verificar sincronización NTP: {ex.Message}");
        }

        return (false, null, null);
    }

    /// <summary>
    /// Retrieves the most recent system time change event and its associated reason.
    /// </summary>
    /// <remarks>This method queries the Windows Event Log for the most recent time change event. It is only
    /// supported on Windows platforms. The method uses the "Microsoft-Windows-Kernel-General" event provider and
    /// searches for events with Event ID 1.</remarks>
    /// <returns>A tuple containing the date and time of the last system time change and a string describing the reason for the
    /// change. If the operating system is not Windows, the date will be <see langword="null"/> and the reason will
    /// indicate that the information is only available on Windows. If no time change events are found, the date will be
    /// <see langword="null"/> and the reason will indicate that no events were found. If an error occurs while reading
    /// the event log, the date will be <see langword="null"/> and the reason will contain the error message.</returns>
    public static (DateTime? changeDate, string changeReason, string details) GetLastTimeChangeReason()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return (null, "Información solo disponible para Windows", string.Empty);
        
        // Buscar eventos de cambio de hora (ID 1) y sincronización de hora (ID 35)
        string query = "*[System[(Provider[@Name='Microsoft-Windows-Kernel-General'] and (EventID=1)) or " +
            "(Provider[@Name='Microsoft-Windows-Time-Service'] and (EventID=35 or EventID=37 or EventID=158))]]";

        try
        {
            var elQuery = new EventLogQuery("System", PathType.LogName, query) {
                ReverseDirection = true // Buscar desde el evento más reciente
            };

            using var reader = new EventLogReader(elQuery);
            EventRecord eventInstance = reader.ReadEvent();
            if (eventInstance != null)
            {
                string mensaje = eventInstance.FormatDescription();
                DateTime? changeDate = eventInstance.TimeCreated;
                int eventId = eventInstance.Id;
                string provider = eventInstance.ProviderName;
                string changeReason = ExtractChangeReason(mensaje, eventId, provider);
                string details = $"EventID: {eventId}, Provider: {provider}";

                return (changeDate, changeReason, details);
            }
        }
        catch (Exception ex) {
            return (null, "Error leyendo eventos de Windows: " + ex.Message, ex.ToString());
        }

        return (null, "No se encontraron eventos de cambio de hora.", string.Empty);
    }

    public static string GetLastTimeChangeReasonViaPowerShell()
    {
        string script = @"
            Get-WinEvent -LogName System |
            Where-Object { $_.Id -eq 1 -and $_.ProviderName -eq 'Microsoft-Windows-Kernel-General' } |
            Select-Object -First 1 |
            Format-List -Property TimeCreated, Message
        ";

        try
        {
            var startInfo = new ProcessStartInfo {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{script}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        catch (Exception ex) {
            return $"Error ejecutando PowerShell: {ex.Message}";
        }
    }

    private static string ExtractChangeReason(string mensaje, int eventId, string provider)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return "Motivo desconocido";
        
        // Eventos del servicio de tiempo de Windows
        if (provider.Contains("Time-Service", StringComparison.OrdinalIgnoreCase))
        {
            if (eventId == 35)
                return "Sincronización NTP exitosa";
            if (eventId == 37)
                return "Sincronización NTP fallida";
            if (eventId == 158)
                return "Servicio de tiempo iniciado";
        }

        // Eventos del kernel
        if (provider.Contains("Kernel-General", StringComparison.OrdinalIgnoreCase))
        {
            if (mensaje.Contains("El usuario o proceso cambió manualmente", StringComparison.OrdinalIgnoreCase) ||
                mensaje.Contains("changed the system time", StringComparison.OrdinalIgnoreCase))
                return "Cambio manual";

            if (mensaje.Contains("System time synchronized", StringComparison.OrdinalIgnoreCase) ||
                mensaje.Contains("reloj de hardware", StringComparison.OrdinalIgnoreCase) ||
                mensaje.Contains("hardware clock", StringComparison.OrdinalIgnoreCase))
                return "Sincronización automática";
        }

        return "Motivo no determinado";
    }

    private static string? ExtractNtpServerFromOutput(string output)
    {
        try
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Source:"))
                {
                    return line.Split(':')[1].Trim();
                }
            }
        }
        catch { }

        return null;
    }

    private static string? GetRecentTimeRelatedProcess()
    {
        try
        {
            var timeRelatedKeywords = new[] { "time", "clock", "w32tm", "date", "ntp", "sync", "hora", "fecha" };

            var processes = Process.GetProcesses()
                .Where(p => {
                    try {
                        string name = p.ProcessName.ToLowerInvariant();
                        return timeRelatedKeywords.Any(keyword => name.Contains(keyword));
                    }
                    catch { return false; }
                })
                .OrderByDescending(p => {
                    try { return p.StartTime; }
                    catch { return DateTime.MinValue; }
                })
                .Take(3) // Tomar los 3 más recientes
                .Select(p => p.ProcessName)
                .ToList();

            return processes.Count > 0 ? string.Join(", ", processes) : null;
        }
        catch
        {
            return null;
        }
    }

    // Métodos auxiliares
    private static string? GetCurrentUser()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return WindowsIdentity.GetCurrent()?.Name;
            }
        }
        catch {}

        return Environment.UserName;
    }

    private static string GetWindowsVersion()
    {
        try
        {
            return Environment.OSVersion.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static async Task<bool> IsNetworkConnectedAsync()
    {
        try
        {
            return await Task.Run(() => NetworkInterface.GetIsNetworkAvailable());
        }
        catch
        {
            return false;
        }
    }

    private static string GetNtpSyncType()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Unknown";

            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\W32Time\Parameters");
            return key?.GetValue("Type")?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Error";
        }
    }

    private static bool IsWindowsTimeServiceRunning()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = "query w32time",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("RUNNING");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the system is configured to synchronize time using an NTP server.
    /// </summary>
    /// <remarks>This method checks the system's registry settings to determine if an NTP server is
    /// configured.  It only applies to Windows platforms and will return <see langword="false"/> for non-Windows
    /// systems  or if an error occurs while accessing the registry.</remarks>
    /// <returns><see langword="true"/> if the system is configured to use an NTP server other than the default  Windows time
    /// server ("time.windows.com,0x9"); otherwise, <see langword="false"/>.</returns>
    private static bool IsNtpSyncEnabled()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            // Solo acceder al registro si estamos en Windows
            // Ruta completa: Equipo\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\W32Time\Parameters
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\W32Time\Parameters");

            var ntpServer = key?.GetValue("NtpServer")?.ToString();
            var value = key?.GetValue("Type")?.ToString();

            // Evalúa si el tipo indica sincronización automática
            //if (!string.IsNullOrEmpty(value))
            //{
            //    return value.Equals("NTP", StringComparison.OrdinalIgnoreCase) ||
            //           value.Equals("AllSync", StringComparison.OrdinalIgnoreCase) ||
            //           value.Equals("Nt5DS", StringComparison.OrdinalIgnoreCase);
            //}

            return !string.IsNullOrEmpty(ntpServer) && ntpServer != "time.windows.com,0x9";
        }
        catch {
            return false;
        }
    }

    /// <summary>
    /// Retrieves the system uptime in seconds.
    /// </summary>
    /// <returns>The total number of seconds the system has been running since the last restart.  Returns 0 if the uptime cannot
    /// be determined.</returns>
    private static long GetSystemUptime()
    {
        try
        {
            return (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime? GetLastBootTime()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
                var managementObjects = searcher.Get();

                foreach (ManagementObject mo in managementObjects.Cast<ManagementObject>())
                {
                    var bootTime = mo["LastBootUpTime"].ToString();
                    return ManagementDateTimeConverter.ToDateTime(bootTime);
                }
            }
        }
        catch { }

        return null;
    }

    public void Dispose()
    {
        if (_isInitialized && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SystemEvents.TimeChanged -= OnTimeChanged!;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged!;
        }

        _offlineManager?.Dispose();
    }
}

// Clases de apoyo
public class TimeChangeEventArgs
{
    public string? EventType { get; set; }
    public DateTime NewTime { get; set; }
    public DateTime? PreviousTime { get; set; }
}