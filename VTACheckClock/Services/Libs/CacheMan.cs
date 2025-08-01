using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VTACheckClock.Models;

namespace VTACheckClock.Services.Libs
{
    /// <summary>
    /// Enumeración de tipos de caché.
    /// </summary>
    public enum CacheType
    {
        Unknown = 0,
        Notices = 1,
        Employees = 2,
        History = 3,
        Punches = 4,
        FIDs = 5
    }

    /// <summary>
    /// Clase para almacenar la información en tiempo de ejecución de los archivos del caché.
    /// </summary>
    public class CacheFileData
    {
        private CacheType _file_type;
        private string _file_uuid;

        public int file_offid { get; set; }
        public DateTime file_time { get; set; }
        public FileInfo? file_info { get; set; }
        public string file_uuid { get => _file_uuid; }

        public CacheType FileType
        {
            get => _file_type;

            set
            {
                if (Enum.IsDefined(typeof(CacheType), value))
                {
                    _file_type = value;
                }

                else
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        public CacheFileData()
        {
            _file_uuid = GlobalVars.clockSettings?.clock_uuid ?? Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Clase que administrará el caché del sistema.
    /// </summary>
    class CacheMan
    {
        private static readonly Logger log = LogManager.GetLogger("app_logger");

        public enum CacheDirs
        {
            cache,
            current,
            old,
            fids
        };

        /// <summary>
        /// Contantes.
        /// </summary>
        private const string FixedFileHeader = "VCCH";
        private const string CacheFileStart = "VTCacheFile";
        private const string DefCachePrefix = "vtattcache_";
        private const string DefCacheFileExt = ".vcch";
        private const string DefTempFileExt = ".vtmp";
        private static readonly string[] CacheNames = [
          "00000000", // Desconocido
          "4E6F7411", // Avisos
          "456D7012", // Empleados y FMDs
          "48697321", // Histórico
          "50756E31", // Checadas
          "46494441"  // FIDs
        ];

        public static readonly List<(string Id, string Name)> CacheFileNames = [
            ("00000000", "Desconocido"),
            ("4E6F7411", "Avisos"),
            ("456D7012", "Empleados y FMDs"),
            ("48697321", "Histórico"),
            ("50756E31", "Checadas"),
            ("46494441", "FIDs")
        ];

        /// <summary>
        /// Campos de la clase.
        /// </summary>
        private int old_notices_records;
        private int old_employees_records;
        private int old_history_records;
        private readonly bool create_success;
        private DirectoryInfo? cacheRoot;
        private DirectoryInfo? currentCacheSubdir;
        private DirectoryInfo? oldCacheSubdir;
        private DirectoryInfo? previousPunchesSubdir;
        private CacheFileData? notices_file;
        private CacheFileData? employees_file;
        private CacheFileData? history_file;
        private CacheFileData? punches_file;
        private CacheFileData? old_notices;
        private CacheFileData? old_employees;
        private CacheFileData? old_history;
        private CacheFileData? old_punches;
        private OfficeData? work_office;
        private List<CacheFileData>? OldCachedPunches;

        /// <summary>
        /// Getters y Setters.
        /// </summary>
        public int OldHistoryRecords { get => old_history_records; }
        public int OldEmployeesRecords { get => old_employees_records; }
        public int OldNoticesRecords { get => old_notices_records; }
        public bool CreateSuccess { get => create_success; }
        public DirectoryInfo? CacheRoot { get => cacheRoot; }
        public DirectoryInfo? CurrentCacheSubdir { get => currentCacheSubdir; }
        public DirectoryInfo? OldCacheSubdir { get => oldCacheSubdir; }
        public CacheFileData? NoticesFile { get => notices_file; }
        public CacheFileData? EmployeesFile { get => employees_file; }
        public CacheFileData? HistoryFile { get => history_file; }
        public CacheFileData? PunchesFile { get => punches_file; }
        public CacheFileData? OldNotices { get => old_notices; }
        public CacheFileData? OldEmployees { get => old_employees; }
        public CacheFileData? OldPunches { get => old_punches; }
        public CacheFileData? OldHistory { get => old_history; }
        
        /// <summary>
        /// Método de inicialización de la clase.
        /// </summary>
        /// <param name="the_office">La información de la oficina configurada en el sistema.</param>
        public CacheMan(OfficeData? the_office = null)
        {
            create_success = false;
            work_office = the_office ?? GlobalVars.this_office;

            bool b1 = InitCacheFolders();
            bool b2 = InitOldCache();
            bool b3 = InitCurrentCache();
            RescuePreviousPunches();

            create_success = b1 && b2 && b3;
        }

        /// <summary>
        /// Inicializa los directorios de almacenamiento del caché.
        /// </summary>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        private bool InitCacheFolders()
        {
            try {
                cacheRoot = Directory.CreateDirectory(GlobalVars.DefWorkPath + @"\" + ((CacheDirs)0).ToString());
                currentCacheSubdir = Directory.CreateDirectory(CacheRoot.FullName + @"\" + ((CacheDirs)1).ToString());
                oldCacheSubdir = Directory.CreateDirectory(CacheRoot.FullName + @"\" + ((CacheDirs)2).ToString());
                previousPunchesSubdir = Directory.CreateDirectory(GlobalVars.DefWorkPath);

                return true;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Inicializa el caché actual y pobla los archivos con los registros actualizados.
        /// </summary>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        private bool InitCurrentCache()
        {
            bool f1 = CreateCacheFile(CacheType.Notices, out notices_file);
            bool f2 = CreateCacheFile(CacheType.Employees, out employees_file);
            bool f3 = CreateCacheFile(CacheType.History, out history_file);
            bool f4 = CreateCacheFile(CacheType.Punches, out punches_file);

            return f1 && f2 && f3 && f4;
        }

        /// <summary>
        /// Inicializa el caché histórico.
        /// </summary>
        /// <returns>True si la operación se llevó a cabo con éxito. False de lo contrario.</returns>
        private bool InitOldCache()
        {
            ConsolidateOldCache();
            PurgeOldCache();

            return true;
        }

        /// <summary>
        /// Consolida los archivos del caché histórico.
        /// </summary>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        private bool ConsolidateOldCache()
        {
            MergeOldCache();
            MergeOldPunches();

            return true;
        }

        /// <summary>
        /// Busca todos los archivos ya existentes en los directorios de trabajo del caché, los consolida, los mueve al directorio histórico y los indexa.
        /// </summary>
        /// <returns>True si la óperación se completó con éxito. False de lo contrario.</returns>
        private bool MergeOldCache()
        {
            try {
                CacheRoot.Refresh();
                CurrentCacheSubdir.Refresh();
                OldCacheSubdir.Refresh();

                List<FileInfo> tempo_list = [];
                FileInfo[] files_basedir = CacheRoot.GetFiles("*.*", SearchOption.TopDirectoryOnly);
                FileInfo[] files_workdir = CurrentCacheSubdir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                tempo_list.AddRange(files_basedir);
                tempo_list.AddRange(files_workdir);

                FileInfo[] files_merged = [.. tempo_list];

                foreach (FileInfo the_file in files_merged)
                {
                    if (the_file.Extension != DefCacheFileExt) {
                        the_file.Delete();
                    } else {
                        int consec = 0;
                        string filename = the_file.Name;
                        string fileext = the_file.Extension;
                        string baldfilename = filename.Substring(0, filename.Length - fileext.Length);
                        string actualfilepath = the_file.FullName;
                        string newfilepath = OldCacheSubdir.FullName + @"\" + filename;
                        string filenameroot = baldfilename.Split(['-'])[0];

                        while (File.Exists(newfilepath))
                        {
                            newfilepath = OldCacheSubdir.FullName + @"\" + filenameroot + @"-" + consec.ToString("X4") + DefCacheFileExt;
                            consec++;
                        }

                        File.Move(actualfilepath, newfilepath);
                    }
                }

                FileInfo[] files_olddir = OldCacheSubdir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                if (files_olddir.Length > 0)
                {
                    OldCachedPunches = null;
                    OldCachedPunches = [];

                    foreach (FileInfo el_file in files_olddir)
                    {
                        if (el_file.Extension != DefCacheFileExt)
                        {
                            el_file.Delete();
                        } else {
                            CacheFileData? f_data = ParseFileHeader(el_file);

                            if (f_data != null)
                            {
                                if (f_data.FileType == CacheType.Punches)
                                {
                                    OldCachedPunches.Add(f_data);
                                }
                            } else {
                                File.Delete(el_file.FullName);
                            }
                        }
                    }
                }

                return true;
            } catch(Exception ex) {
                log.Warn("MergeOldCache error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Consolida todos los registros de asistencia de los archivos del caché histórico y los consolida en una solo.
        /// </summary>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        private bool MergeOldPunches()
        {
            try
            {
                if (OldCachedPunches.Count > 0)
                {
                    string f_name = OldCacheSubdir.FullName + @"\" + DefCachePrefix + CacheNames[(int)CacheType.Punches] + "_merged" + DefCacheFileExt;
                    string newfilepath = f_name;
                    FileInfo f_info;
                    CacheFileData f_data;

                    if (File.Exists(f_name))
                    {
                        int consec = -1;

                        while (File.Exists(newfilepath))
                        {
                            consec++;
                            newfilepath = OldCacheSubdir.FullName + @"\" + DefCachePrefix + CacheNames[(int)CacheType.Punches] + "_merged" + @"-" + consec.ToString("X4") + DefCacheFileExt;
                        }

                        File.Move(f_name, newfilepath);
                        MergeOldCache();
                    }

                    using (FileStream fs = File.Create(f_name))
                    {
                        f_info = new FileInfo(f_name);
                    }

                    f_data = CreateFileHeader(f_info, CacheType.Punches, out string f_header);
                    File.WriteAllLines(f_data.file_info.FullName, [FixedFileHeader + CommonProcs.EnDeCapsulateTxt(f_header, true)]);
                    CopyPunches(OldCachedPunches, f_data, true);
                    old_punches = f_data;
                    OldCachedPunches = null;

                    OldCachedPunches = [f_data];

                    return true;
                } else {
                    return true;
                }
            } catch(Exception ex) {
                log.Warn("MergeOldPunches error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Copia registro de asistencia de una lista de archivos de caché a un archivo de caché diferente. Todos los archivos deben ya existir y contar con el encabezado correspondiente.
        /// </summary>
        /// <param name="source_files">Lista de objetos de información de caché de los archivos origen.</param>
        /// <param name="dest_file">Objeto de inforación de caché del archivo destino.</param>
        /// <param name="del_after">Indica si los archivos fuente deberán ser borrados del disco al completar la copia.</param>
        /// <returns>True si la operación se realizó con éxtio. False de lo contrario.</returns>
        private static bool CopyPunches(List<CacheFileData> source_files, CacheFileData dest_file, bool del_after = false)
        {
            try
            {
                if ((source_files == null) || (source_files.Count < 1))
                {
                    return false;
                }

                foreach (CacheFileData src_file in source_files)
                {
                    File.AppendAllLines(dest_file.file_info.FullName, File.ReadLines(src_file.file_info.FullName).Skip(1));
                    if (del_after) File.Delete(src_file.file_info.FullName);
                }

                return true;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Purga todos los archivos repetidos del caché histórico, conservando sólo los más recientes.
        /// </summary>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        private bool PurgeOldCache()
        {
            try
            {
                string f_name = string.Empty;
                string old_filename = string.Empty;
                string new_filename = string.Empty;
                List<CacheFileData> files_data = [];

                DataTable files_table = new() {
                    Columns = { "type", "uuid", "timestamp", "name" }
                };

                FileInfo[] files_olddir = OldCacheSubdir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                if (files_olddir.Length > 0)
                {
                    foreach (FileInfo el_file in files_olddir)
                    {
                        if (el_file.Extension != DefCacheFileExt)
                        {
                            el_file.Delete();
                        } else {
                            CacheFileData? f_data = ParseFileHeader(el_file);

                            if (f_data != null) {
                                if (f_data.FileType != CacheType.Punches)
                                {
                                    files_data.Add(f_data);
                                }
                            }
                        }
                    }
                }

                foreach (CacheFileData elfile_data in files_data)
                {
                    files_table.Rows.Add((int)elfile_data.FileType, elfile_data.file_uuid, elfile_data.file_time.ToString("yyyyMMddHHmmss"), elfile_data.file_info.FullName);
                }

                int[] los_types = [(int)CacheType.Notices, (int)CacheType.Employees, (int)CacheType.History];

                foreach (int el_tipo in los_types)
                {
                    int preserved_idx = -1;
                    int file_records = 0;
                    int preserved_records = 0;
                    FileInfo? preserved_info = null;
                    FileInfo? f_info = null;
                    DataTable dt = (new DataView(files_table, ("type = " + el_tipo.ToString()), "timestamp DESC", DataViewRowState.CurrentRows)).ToTable();

                    if (dt.Rows.Count > 0)
                    {
                        for (int ii = 0; ii < dt.Rows.Count; ii++)
                        {
                            var file_name = dt.Rows[ii]["name"].ToString() ?? "";
                            f_info = new FileInfo(file_name);
                            file_records = CountFileRecords(f_info);

                            if ((preserved_idx < 0) && (file_records > 0))
                            {
                                preserved_idx = ii;
                                preserved_records = file_records;
                                preserved_info = f_info;
                            }

                            if (ii != preserved_idx) File.Delete(file_name);
                        }
                    }

                    CacheFileData? file_d = (preserved_info != null) ? ParseFileHeader(preserved_info) : null;

                    switch ((CacheType)el_tipo)
                    {
                        case CacheType.Notices:
                            old_notices = file_d;
                            old_notices_records = preserved_records;
                            break;

                        case CacheType.Employees:
                            old_employees = file_d;
                            old_employees_records = preserved_records;
                            break;

                        case CacheType.History:
                            old_history = file_d;
                            old_history_records = preserved_records;
                            break;

                        default:
                            break;
                    }
                }

                return true;
            } catch {
                return false;
            }
        }


        /// <summary>
        /// Cuenta la cantidad de registros almacenados en un archivo de caché específico (sin contar la línea de encabezado).
        /// </summary>
        /// <param name="el_file">Objeto FileInfo del archivo que se examinará.</param>
        /// <returns>Cantidad de registros encontrados.</returns>
        private static int CountFileRecords(FileInfo el_file)
        {
            return File.ReadLines(el_file.FullName).Count() - 1;
        }

        /// <summary>
        /// Recupera los registros de asistencia que llegue a encontrar almacenados en el formato anterior del caché y los reintegra al nuevo caché.
        /// </summary>
        private void RescuePreviousPunches()
        {
            previousPunchesSubdir.Refresh();

            List<FileInfo> prev_files = [];
            FileInfo[] files_1 = previousPunchesSubdir.GetFiles(("*" + DefCacheFileExt), SearchOption.TopDirectoryOnly);
            FileInfo[] files_2 = previousPunchesSubdir.GetFiles(("*" + DefTempFileExt), SearchOption.TopDirectoryOnly);
            prev_files.AddRange(files_1);
            prev_files.AddRange(files_2);

            foreach (FileInfo f_info in prev_files)
            {
                if (f_info.Length > 0)
                {
                    try
                    {
                        foreach (string la_line in File.ReadLines(f_info.FullName))
                        {
                            string[] la_punch = CommonProcs.UnZipString(la_line).Split('|');

                            StorePunch(
                                new PunchLine {
                                    Punchemp = int.Parse(la_punch[0]),
                                    Punchevent = int.Parse(la_punch[1]),
                                    Punchtime = Convert.ToDateTime(la_punch[2])
                                }
                            );
                        }
                    } catch {
                        continue;
                    }
                }

                File.Delete(f_info.FullName);
            }
        }

        /// <summary>
        /// Crea el archivo temporal para almacenar el caché del tipo solicitado.
        /// </summary>
        /// <param name="cache_type">Tipo de caché para el cual se creará el archivo.</param>
        /// <param name="file_data">Parámetro de salida que contendrá la información del archivo creado.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        private bool CreateCacheFile(CacheType cache_type, out CacheFileData? file_data, DirectoryInfo? el_cachedir = null)
        {
            try
            {
                el_cachedir ??= CurrentCacheSubdir;
                el_cachedir.Refresh();

                int el_consec = -1;
                string file_fullname = string.Empty;
                string file_suffix = string.Empty;
                string file_prefix = DefCachePrefix + CacheNames[(int)cache_type] + @"-";
                bool file_exists = true;
                FileInfo? file_info;

                while (file_exists)
                {
                    el_consec++;
                    file_suffix = el_consec.ToString("X4");
                    file_fullname = el_cachedir.FullName + @"\" + file_prefix + file_suffix + DefCacheFileExt;
                    file_exists = File.Exists(file_fullname);
                }

                using (FileStream fs = File.Create(file_fullname))
                {
                    file_info = new FileInfo(file_fullname);
                }

                file_data = CreateFileHeader(file_info, cache_type, out string header_line);
                File.WriteAllLines(file_data.file_info.FullName, [(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(header_line, true))]);

                return true;
            } catch {
                file_data = null;
                return false;
            }
        }

        /// <summary>
        /// Crea el encabezado y el objeto de información del archivo de caché.
        /// </summary>
        /// <param name="el_file">Objeto FileInfo del archivo para el que será creado el encabezado.</param>
        /// <param name="cache_type">El tipo de caché al que pertenece el archivo.</param>
        /// <param name="header_line">Parámetro de salida que contendrá la línea que se escribirá en el encabezado del archivo.</param>
        /// <returns>Objeto con la información del archivo de caché.</returns>
        private CacheFileData CreateFileHeader(FileInfo? el_file, CacheType cache_type, out string header_line)
        {
            DateTime filetime = DateTime.Now;
            int off_id = (work_office != null) ? work_office.Offid : 0;
            string? off_name = (work_office != null) ? work_office.Offname : string.Empty;

            header_line = CacheFileStart + "|" + ((int)cache_type).ToString() + "|" + filetime.ToString("yyyyMMddHHmmss") + "|" + off_id.ToString() + "|" + off_name;

            return new CacheFileData
            {
                file_offid = off_id,
                file_time = filetime,
                file_info = el_file,
                FileType = cache_type
            };
        }

        /// <summary>
        /// Decodifica la línea de encabezado del archivo y crea un objeto de información de archivo de caché.
        /// </summary>
        /// <param name="el_file">Objeto FileInfo del archivo que se leerá.</param>
        /// <returns>Objeto con la información del archivo de caché.</returns>
        private static CacheFileData? ParseFileHeader(FileInfo el_file)
        {
            el_file.Refresh();

            if ((!File.Exists(el_file.FullName)) || (el_file.Length <= 0))
            {
                return null;
            }

            try {
                string? header_line = string.Empty;
                string[] header_parts;
                DateTime timestamp;

                foreach (string la_line in File.ReadLines(el_file.FullName))
                {
                    header_line = la_line;
                    break; //Just take the first header line and finish foreach
                }

                header_line = header_line.Substring(FixedFileHeader.Length);
                header_line = CommonProcs.EnDeCapsulateTxt(header_line, false);
                header_parts = header_line.Split(['|']);

                timestamp = CommonProcs.FromFileString(header_parts[2]);

                if (timestamp == DateTime.MinValue) return null;

                if (header_parts[0] != CacheFileStart) return null;

                if (!(int.TryParse(header_parts[1], out int cachetype) && int.TryParse(header_parts[3], out int offid)))
                {
                    return null;
                }

                return new CacheFileData {
                    file_offid = offid,
                    FileType = (CacheType)cachetype,
                    file_info = el_file,
                    file_time = timestamp
                };
            } catch(Exception ex) {
                log.Warn("ParseFileHeader error: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Recupera los registros de asistencia almacenados en el caché.
        /// </summary>
        /// <param name="cache_source">Indica de qué repositorio del caché se leerán los registros de asistencia. 0 = para el caché actual, 1 = para el caché histórico, 2 = para ambos.</param>
        /// <returns>Un arreglo de cadenas de texto con cada uno de los registros de asistencia recuperados.</returns>
        public string[] GetCachedPunches(int cache_source = 0)
        {
            try {
                int ii = 0;
                List<string> all_punches = [];
                List<string> new_punches = [];
                List<string> old_punches = [];

                switch (cache_source) {
                    case 2:
                        new_punches = [.. File.ReadLines(PunchesFile.file_info.FullName).Skip(1)];
                        old_punches = (OldPunches != null) ? [.. File.ReadLines(OldPunches.file_info.FullName).Skip(1)] : old_punches;
                        break;

                    case 1:
                        old_punches = (OldPunches != null) ? [.. File.ReadLines(OldPunches.file_info.FullName).Skip(1)] : old_punches;
                        break;

                    case 0:
                        new_punches = [.. File.ReadLines(PunchesFile.file_info.FullName).Skip(1)];
                        break;

                    default:
                        new_punches = [.. File.ReadLines(PunchesFile.file_info.FullName).Skip(1)];
                        break;
                }

                // Ignorar líneas vacías, nulas o con espacios en blanco. Tambien, omite las lineas con caracteres vacios
                // Se usa expresion de Coleccion en vez de .toList();
                var empty_lines = old_punches.Where(line => string.IsNullOrWhiteSpace(line) || line.All(c => c == '\0')).ToList();
                if (empty_lines.Count > 0) {
                    log.Warn("GetCachedPunches: Found empty lines in new punches cache.");
                    foreach (var empty_line in empty_lines) {
                        log.Warn("Empty line: " + empty_line);
                    }
                }

                old_punches = [.. old_punches.Where(line => !string.IsNullOrWhiteSpace(line) && !line.All(c => c == '\0'))];

                all_punches.AddRange(new_punches);
                all_punches.AddRange(old_punches);

                string[] la_resp = new string[all_punches.Count];
                foreach (string str in all_punches) {
                    la_resp[ii] = CommonProcs.EnDeCapsulateTxt(str, false) ?? "";
                    ii++;
                }

                return la_resp;
            } catch(Exception ex) {
                log.Warn("GetCachedPunches error: " + ex.Message);
                return [];
            }
        }

        /// <summary>
        /// Almacena un registro de asistencia en el caché de la aplicación.
        /// </summary>
        /// <param name="la_punch">El objeto que contiene la información del registro de asistencia.</param>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        public bool StorePunch(PunchLine la_punch)
        {
            try {
                string la_punchline = 
                    la_punch.Punchemp.ToString() + "|" + 
                    la_punch.Punchevent.ToString() + "|" + 
                    la_punch.Punchtime.ToString("yyyy/MM/dd HH:mm:ss") + "|" + 
                    la_punch.Punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss");

                File.AppendAllLines(PunchesFile.file_info.FullName, [CommonProcs.EnDeCapsulateTxt(la_punchline, true) ?? ""]);

                return true;
            } catch(Exception ex) {
                log.Warn("Error al agregar el registro en la Cache: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Limpia el historial de registros de asistencia almacenados en caché. (Para ser utilizado tras una sincronización correcta con el servidor).
        /// </summary>
        /// <param name="cache_source">Inidica de qué repositorio del caché se leerán los registros de asistencia. 0 = para el caché actual, 1 = para el caché histórico, 2 = para ambos.</param>
        /// <returns>True si la operación se realizó con éxito. False de lo contrario.</returns>
        public bool PurgeCachedPunches(int cache_source = 0)
        {
            string oldp_header;
            string newp_header;
            CacheFileData old_punches_file;
            CacheFileData new_punches_file;

            try
            {
                switch (cache_source) {
                    case 2:
                        if (OldPunches != null)
                        {
                            old_punches_file = CreateFileHeader(OldPunches.file_info, CacheType.Punches, out oldp_header);
                            File.WriteAllLines(old_punches_file.file_info.FullName, ([(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(oldp_header, true))]));
                            old_punches = old_punches_file;
                        }

                        new_punches_file = CreateFileHeader(PunchesFile.file_info, CacheType.Punches, out newp_header);
                        File.WriteAllLines(new_punches_file.file_info.FullName, ([(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(newp_header, true))]));
                        punches_file = new_punches_file;

                        break;

                    case 1:

                        if (OldPunches != null)
                        {
                            old_punches_file = CreateFileHeader(OldPunches.file_info, CacheType.Punches, out oldp_header);
                            File.WriteAllLines(old_punches_file.file_info.FullName, ([(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(oldp_header, true))]));
                            old_punches = old_punches_file;
                        }

                        break;

                    case 0:
                        new_punches_file = CreateFileHeader(PunchesFile.file_info, CacheType.Punches, out newp_header);
                        File.WriteAllLines(new_punches_file.file_info.FullName, ([(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(newp_header, true))]));
                        punches_file = new_punches_file;

                        break;

                    default:
                        new_punches_file = CreateFileHeader(PunchesFile.file_info, CacheType.Punches, out newp_header);
                        File.WriteAllLines(new_punches_file.file_info.FullName, ([(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(newp_header, true))]));
                        punches_file = new_punches_file;

                        break;
                }

                return true;
            }

            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Almacena una lista de objetos de información de aviso en el archivo correspondiente del caché.
        /// </summary>
        /// <param name="las_notices">Lista de objetos de información de archivo que serán almacenados.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public Task<bool> SaveNotices(List<NoticeData> las_notices)
        {
            if (las_notices.Count < 1) return Task.FromResult(true);

            try {
                int ii = 0;
                string[] to_write = new string[las_notices.Count];

                foreach (NoticeData? la_notice in las_notices) {
                    var not_id = la_notice.notid.ToString();
                    var not_it = la_notice.nottit;
                    var not_msg = la_notice.notmsg;
                    var not_img = la_notice.notimg;
                    var notice = $"{not_id}|{not_it}|{not_msg}|{not_img}";

                    to_write[ii] = CommonProcs.EnDeCapsulateTxt(notice, true) ?? "";
                    ii++;
                }

                File.AppendAllLines(NoticesFile.file_info.FullName, to_write);

                return Task.FromResult(true);
            } catch {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Recupera los avisos almacenados en caché.
        /// </summary>
        /// <param name="from_old">Indica si se deberán recuperar del archivo de caché histórico o del actual. True para histórico, false para actual.</param>
        /// <returns>Lista de objetos de información de avisos almacenados en el caché.</returns>
        public List<NoticeData> RetrieveNotices(bool from_old = true)
        {
            List<NoticeData> la_resp = [];

            try {
                string? el_notice = string.Empty;
                string[] notice_parts;
                CacheFileData? el_file = from_old ? OldNotices : NoticesFile;

                foreach (string la_line in File.ReadLines(el_file?.file_info?.FullName!).Skip(1))
                {
                    el_notice = CommonProcs.EnDeCapsulateTxt(la_line, false);
                    if (el_notice != null)
                    {
                        notice_parts = el_notice.Split(['|']);

                        la_resp.Add(
                            new NoticeData {
                                notid = int.Parse(notice_parts[0]),
                                nottit = notice_parts[1],
                                notmsg = notice_parts[2],
                                notimg = notice_parts[3]
                            }
                        );
                    }
                }
            }

            catch
            {
                la_resp.Clear();
            }

            return la_resp;
        }

        /// <summary>
        /// Almacena la información de los empleados y sus huellas dactilares en el archivo correspondiente del caché.
        /// </summary>
        /// <param name="employee_data">DataTable con la información de los empleados y huellas dactilares.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public bool SaveEmployees(DataTable employee_data)
        {
            if (employee_data.Rows.Count < 1) return true;

            string[] valid_cols = ["OffID", "EmpID", "FingerID", "FingerFMD", "EmpNum", "EmpName", "EmpPass"];

            foreach (DataColumn dc in employee_data.Columns)
            {
                if (!valid_cols.Contains(dc.ColumnName)) return false;
            }

            foreach (string str in valid_cols)
            {
                if (!employee_data.Columns.Contains(str)) return false;
            }

            try {
                int ii = 0;
                string[] to_write = new string[employee_data.Rows.Count];

                foreach (DataRow dr in employee_data.Rows)
                {
                    to_write[ii] = CommonProcs.EnDeCapsulateTxt(dr["OffID"].ToString() + "|" + dr["EmpID"].ToString() + "|" + dr["FingerID"].ToString() + "|" + dr["FingerFMD"].ToString() + "|" + dr["EmpNum"].ToString() + "|" + dr["EmpName"].ToString() + "|" + dr["EmpPass"].ToString(), true) ?? "";
                    ii++;
                }

                string fmds_header = string.Empty;
                CacheFileData fmds_file = CreateFileHeader(EmployeesFile.file_info, CacheType.Employees, out fmds_header);
                File.WriteAllLines(fmds_file.file_info.FullName, [FixedFileHeader + CommonProcs.EnDeCapsulateTxt(fmds_header, true)]);
                File.AppendAllLines(fmds_file.file_info.FullName, to_write);
                employees_file = fmds_file;

                return true;
            } catch {
                return false;
            }
        }

        public Task<bool> SaveEmployeesAsync(DataTable employee_data)
        {
            if (employee_data.Rows.Count < 1) return Task.FromResult(true);

            string[] valid_cols = ["OffID", "EmpID", "FingerID", "FingerFMD", "EmpNum", "EmpName", "EmpPass"];

            foreach (DataColumn dc in employee_data.Columns)
            {
                if (!valid_cols.Contains(dc.ColumnName)) return Task.FromResult(false);
            }

            foreach (string str in valid_cols)
            {
                if (!employee_data.Columns.Contains(str)) return Task.FromResult(false);
            }

            try {
                int ii = 0;
                string[] to_write = new string[employee_data.Rows.Count];

                foreach (DataRow dr in employee_data.Rows)
                {
                    to_write[ii] = CommonProcs.EnDeCapsulateTxt(dr["OffID"].ToString() + "|" + dr["EmpID"].ToString() + "|" + dr["FingerID"].ToString() + "|" + dr["FingerFMD"].ToString() + "|" + dr["EmpNum"].ToString() + "|" + dr["EmpName"].ToString() + "|" + dr["EmpPass"].ToString(), true) ?? "";
                    ii++;
                }

                string fmds_header = string.Empty;
                CacheFileData fmds_file = CreateFileHeader(EmployeesFile.file_info, CacheType.Employees, out fmds_header);
                File.WriteAllLines(fmds_file.file_info.FullName, [FixedFileHeader + CommonProcs.EnDeCapsulateTxt(fmds_header, true)]);
                File.AppendAllLines(fmds_file.file_info.FullName, to_write);
                employees_file = fmds_file;

                return Task.FromResult(true);
            } catch {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Recupera la información de los empleados almacenada en el caché.
        /// </summary>
        /// <param name="from_old">Indica si se deberán recuperar del archivo de caché histórico o del actual. True para histórico, false para actual.</param>
        /// <returns>Objeto DataTable con información de los empleados y sus huellas dactilares.</returns>
        public DataTable RetrieveEmployees(bool from_old = true)
        {
            DataTable la_resp = CommonObjs.VoidFMDs;

            try {
                string? el_employee = string.Empty;
                string[] emp_parts;
                CacheFileData? el_file = (from_old) ? OldEmployees : EmployeesFile;

                foreach (string la_line in File.ReadLines(el_file.file_info.FullName).Skip(1))
                {
                    el_employee = CommonProcs.EnDeCapsulateTxt(la_line, false);
                    emp_parts = el_employee.Split(['|']);

                    la_resp.Rows.Add(int.Parse(emp_parts[0]), emp_parts[1], emp_parts[2], emp_parts[3], emp_parts[4], emp_parts[5], emp_parts[6]);
                }
            } catch {
                la_resp.Rows.Clear();
            }

            return la_resp;
        }

        /// <summary>
        /// Almacena el historial de registros de asistencia recientes en el caché.
        /// </summary>
        /// <param name="history_dt">DataTable con los registros de asistencia.</param>
        /// <returns>True si la operación se completó con éxito. False de lo contrario.</returns>
        public Task<bool> SaveHistory(DataTable history_dt)
        {
            if (history_dt.Rows.Count < 1) return Task.FromResult(true);

            string[] valid_cols = ["EmpID", "EvID", "PuncTime", "PuncCalc"];

            foreach (DataColumn dc in history_dt.Columns)
            {
                if (!valid_cols.Contains(dc.ColumnName)) return Task.FromResult(false);
            }

            foreach (string str in valid_cols)
            {
                if (!history_dt.Columns.Contains(str)) return Task.FromResult(false);
            }

            try {
                int ii = 0;
                //Crea un arreglo de cadenas vacias de acuerdo a la longitud del historial de registros de asistencias
                string[] to_write = new string[history_dt.Rows.Count];

                foreach (DataRow dr in history_dt.Rows)
                {
                    to_write[ii] = CommonProcs.EnDeCapsulateTxt((
                        dr["EmpID"].ToString() + "|" + 
                        dr["EvID"].ToString() + "|" + 
                        CommonProcs.ParseValidDT(dr["PuncTime"].ToString() ?? "", "yyyyMMddHHmmss") + "|" + 
                        CommonProcs.ParseValidDT(dr["PuncCalc"].ToString() ?? "", "yyyyMMddHHmmss")
                    ), true) ?? "";

                    ii++;
                }

                string hist_header = string.Empty;
                CacheFileData hist_file = CreateFileHeader(HistoryFile.file_info, CacheType.History, out hist_header);
                //Sobreescribe el contenido del archivo y escribe la primera linea del Encabezado.
                File.WriteAllLines(hist_file.file_info.FullName, [(FixedFileHeader + CommonProcs.EnDeCapsulateTxt(hist_header, true))]);
                //Agrega nuevas lineas de contenido al final del encabezado
                File.AppendAllLines(hist_file.file_info.FullName, to_write);
                history_file = hist_file;

                return Task.FromResult(true);
            } catch {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Recupera el historial de registros de asistencia recientes almacenado en el caché.
        /// </summary>
        /// <param name="from_old">Indica si se deberán recuperar del archivo de caché histórico o del actual. True para histórico, false para actual.</param>
        /// <returns>Objeto DataTable con el historial de registros de asistencia recientes.</returns>
        public DataTable RetrieveHistory(bool from_old = true)
        {
            DataTable la_resp = CommonObjs.VoidPunches;

            try
            {
                string? punch_record = string.Empty;
                string[] punch_parts;
                CacheFileData? el_file = from_old ? OldHistory : HistoryFile;

                foreach (string la_line in File.ReadLines(el_file.file_info.FullName).Skip(1))
                {
                    punch_record = CommonProcs.EnDeCapsulateTxt(la_line, false);
                    punch_parts = punch_record.Split(['|']);

                    int EmpID = int.Parse(punch_parts[0]);
                    int EvID = int.Parse(punch_parts[1]);
                    var PuncTime = CommonProcs.FromFileString(punch_parts[2]).ToString("yyyy/MM/dd HH:mm:ss");
                    var PuncCalc = CommonProcs.FromFileString(punch_parts[3]).ToString("yyyy/MM/dd HH:mm:ss");

                    la_resp.Rows.Add(EmpID, EvID, PuncTime, PuncCalc);
                }
            }

            catch(Exception ex) {
                log.Warn("RetrieveHistory error: " + ex.Message);
                la_resp.Rows.Clear();
            }

            return la_resp;
        }

        public static string ReplaceFileName(string fileName)
        {
            // Buscar el ID en el nombre del archivo
            var item = CacheFileNames.FirstOrDefault(x => fileName.Contains(x.Id));

            if (item != default)
            {
                // Reemplazar el ID hexadecimal con el nombre
                return fileName.Replace(item.Id, item.Name);
            }

            return fileName; // Si no encuentra coincidencia, devuelve el original
        }
    }
}
