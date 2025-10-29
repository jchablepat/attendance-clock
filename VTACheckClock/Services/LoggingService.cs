using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Seq;
using VTACheckClock.Models;

namespace VTACheckClock.Services
{
    public interface ILoggingService
    {
        void Initialize();
    }

    /// <summary>
    /// Servicio de logging centralizado que configura NLog y opcionalmente envía logs a Seq.
    /// </summary>
    public class LoggingService : ILoggingService
    {
        public void Initialize()
        {
            var config = new LoggingConfiguration(); //LogManager.Configuration;

            // Obtener información de la oficina
            MainSettings? settings = RegAccess.GetMainSettings();
            ClockSettings? clockSettings = RegAccess.GetClockSettings();
            OfficeData officeInfo = RegAccess.GetOffRegData();

            // ============================================
            // TARGET 1: Archivo local (respaldo)
            // ============================================
            var fileTarget = new FileTarget("logfile")
            {
                FileName = "${basedir}/logs/AppLog.txt",
                CreateDirs = true,
                ArchiveAboveSize = 10240,
                AutoFlush = true,
                KeepFileOpen = false,
                ArchiveFileName = "${basedir}/logs/archive/AppLog.{####}.txt",
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                MaxArchiveFiles = 20,
                Encoding = System.Text.Encoding.UTF8,
                Header = $"=== Logs de {officeInfo?.Offname} (ID: {clockSettings.clock_office}) ==="
            };

            string? seqUrl = settings?.SeqUrl;
            string? seqApiKey = settings?.SeqApiKey;

            // Enriquecer contexto global con información de la oficina y UUID
            if (clockSettings != null)
            {
                // Layout Renderers con valores dinámicos que son evaluados en tiempo de ejecución, por lo que usamos GDC para pasar datos
                GlobalDiagnosticsContext.Set("OfficeId", clockSettings.clock_office.ToString());
                GlobalDiagnosticsContext.Set("ClockUUID", clockSettings.clock_uuid ?? string.Empty);
            }

            if (officeInfo != null)
            {
                GlobalDiagnosticsContext.Set("OfficeName", officeInfo?.Offname ?? string.Empty);
            }

            // ============================================
            // TARGET 2: Seq (Logs centralizados en la nube)
            // ============================================

            // Configurar Seq si hay valores definidos en configuraciones
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                var seqTarget = new SeqTarget()
                {
                    Name = "seq",
                    ServerUrl = seqUrl,
                    ApiKey = string.IsNullOrWhiteSpace(seqApiKey) ? null : seqApiKey, // Opcional: tu API Key de Seq si tienes configurada
                    Properties = // Incluir propiedades personalizadas que aparecerán en Seq y permitiran filtrar
                    {
                        // Valores estáticos (directo)
                        new SeqPropertyItem { Name = "Application", Value = "VTACheckClock" },
                        new SeqPropertyItem { Name = "Version", Value = "1.0.0" },

                        // Valores que vienen de NLog automáticamente
                        new SeqPropertyItem { Name = "Maquina", Value = "${machinename}" },
                        new SeqPropertyItem { Name = "Usuario", Value = "${environment-user}" },
                        new SeqPropertyItem { Name = "SO", Value = "${environment:PROCESSOR_ARCHITECTURE}" },

                        // Valores de oficina (layout renderer por si cambias oficina en runtime)
                        new SeqPropertyItem { Name = "OfficeId", Value = "${gdc:item=OfficeId}" },
                        new SeqPropertyItem { Name = "ClockUUID", Value = "${gdc:item=ClockUUID}" },
                        new SeqPropertyItem { Name = "OfficeName", Value = "${gdc:item=OfficeName}" }
                    },
                };

                config.AddTarget(seqTarget);

                // Enviar todos los logs, puedes ajustar niveles si lo deseas
                config.AddRule(LogLevel.Info, LogLevel.Fatal, seqTarget);
            }

            // ============================================
            // TARGET 3: Console (para debug)
            // ============================================
            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout = "${time}|${level:uppercase=true}|${message}"
            };

            // ============================================
            // TARGET 4: Admin Alerts (errores críticos)
            // ============================================
            var adminAlertTarget = new AdminAlertNLogTarget() { Name = "admin_alerts" };

            // Agregar targets a la configuración
            config.AddTarget(fileTarget);
            config.AddTarget(consoleTarget);
            config.AddTarget(adminAlertTarget);

            // ============================================
            // REGLAS: Definir qué logs van a qué targets
            // ============================================
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
            config.AddRule(new LoggingRule("app_logger", LogLevel.Info, fileTarget));
            config.AddRule(LogLevel.Fatal, LogLevel.Fatal, adminAlertTarget);

            // Solo en Debug mode, mostrar en consola
            #if DEBUG
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            #endif

            // Aplicar configuración
            LogManager.Configuration = config;
            LogManager.ReconfigExistingLoggers();
        }
    }
}