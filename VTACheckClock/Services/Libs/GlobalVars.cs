using System;
using System.Collections.Generic;
using System.Diagnostics;
using VTACheckClock.Models;

namespace VTACheckClock.Services.Libs
{
    class GlobalVars
    {
        #region Constantes
        public const int FMDFormat = 16842753;
        public const string FMDVersion = "1.0.0";
        public const int ClockSetPriv = 2;
        public const int ClockStartPriv = 1;
        public const int ClockSyncPriv = 3;
        public const int AdminSetPriv = 9;
        public static readonly OfficeData? managmntoff = new() { Offid = 0, Offname = "Management Module", Offdesc = "VTAttendance Management Module Application." };
        #endregion
        public static readonly char[] SeparAtor = { '§' };
        public static string AppWorkPath = AppDomain.CurrentDomain.BaseDirectory;
        public static string? SysTempRoot = Environment.GetEnvironmentVariable("APPDATA") ?? "";
        public static string? SysAppRoot = Environment.GetEnvironmentVariable("PROGRAMFILES") ?? "";
        public static string? TempFolder = Environment.GetEnvironmentVariable("TEMP") ?? "";
        public static string TempFTPPath = @"" + TempFolder + @"\VTAManage\tempftp";
        public static string DefWorkPath = @"" + SysTempRoot + @"\VTSoft\VTAttendance";
        public static string DefRegKey = @"VTSoft\VTAttendance\";
        public static int VTAttModule = 0;
        public static int[]? UserPrivileges;
        public static bool StartingUp = true;
        public static bool IsRestart = false;
        public static bool ForceExit = false;
        public static bool BeOffline = false;
        public static bool NoFPReader = false;
        public static bool SyncOnly = false;
        public static bool OfflineInvoked = false;
        public static bool DoReinstall = false;
        public static bool SyncRetryPending = false;
        public static DateTime CachedTime = DateTime.MinValue;
        /// <summary>
        /// Represents the start time of an operation or event.
        /// </summary>
        /// <remarks>This field is static and can be used to store or retrieve the shared start time
        /// across all instances of the containing type. Ensure that the value is properly initialized before accessing
        /// it.</remarks>
        public static DateTime StartTime;
        public static ClockSettings? clockSettings;
        public static MainSettings? mainSettings;
        public static SessionData? mySession = new();
        public static SessionData? clockSession = new();
        public static OfficeData? this_office;
        public static List<ParamData>? sysParams;
        public static CacheMan? AppCache;
        /// <summary>
        /// Global stopwatch to track the running time of the application. Its value is set at the start of the application and can be used to measure elapsed time.
        /// Its value is reset when the application is restarted. And it is used to calculate the total running time of the application.
        /// </summary>
        public static Stopwatch RunningTime = new();
        public static string? TimeZone;
        public static string SERVER_SECRET_KEY = "b2c0f28e80e5c7f8119a8ec128a6478dde7604928620af9eef8f3eb7d22b4bbc";
    }
}
