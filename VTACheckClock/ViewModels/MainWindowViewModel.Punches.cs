using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using VTACheckClock.Helpers;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using VTACheckClock.Views;
using static VTACheckClock.Views.MessageBox;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
namespace VTACheckClock.ViewModels
{
    partial class MainWindowViewModel
    {
        public ClockSettings? c_settings = RegAccess.GetClockSettings() ?? new ClockSettings();
        private DataTable? emp_punches;

        /// <summary>
        /// Recupera los últimos registros de asistencia por empleado, efectuados en la oficina actual.
        /// </summary>
        private async Task GetRecentPunches()
        {
            if (!GlobalVars.BeOffline) {
                emp_punches = await CommonProcs.GetLastPunchesAsync(new ScantRequest { Question = GlobalVars.clockSettings.clock_office.ToString() });

                if (emp_punches == null) {
                    emp_punches = CommonObjs.VoidPunches;
                }
                else if(emp_punches.Columns.Contains("ERROR")) {
                    log.Error("Using cached punches because of error retrieving recent punches: " + emp_punches.Rows[0]["ERROR"].ToString());
                    emp_punches = GlobalVars.AppCache.RetrieveHistory();
                }
                else {
                    await GlobalVars.AppCache.SaveHistory(emp_punches);
                }
            } else {
                emp_punches = GlobalVars.AppCache.RetrieveHistory();
            }
        }

        private async void ShowEventsByEmployee(int selected_inx)
        {
            if (AttsList.Count > 0 && selected_inx != -1) {
                var employee = AttsList[selected_inx];
                var empData = fmd_collection?.Where(x => x.empid == employee.EmpID).First();

                EmpPunches.Clear();

                await Task.Delay(500);
                await UpdateInfoLabels(6, empData.empnum, employee.FullName);
                DataTable dt_punches = await GetEmpPunches(employee.EmpID);

                foreach (DataRow dr in dt_punches.Rows) {
                    DateTime dattim = CommonProcs.FromFileString(dr["PuncTime"].ToString());
                    _ = int.TryParse(dr["EvID"].ToString(), out int el_ev);

                    var data = new EmployeePunch() {
                        punchdate = CommonProcs.UpperFirst(dattim.ToString("dddd dd/MM/yyyy")),
                        punchtime = dattim.ToString("HH:mm:ss"),
                        punchevent = CommonObjs.EvTypes[int.Parse(dr["EvID"].ToString() ?? "0")]
                    };

                    EmpPunches.Add(data);
                }
            }
        }

        private async void PunchIt(int found_inx)
        {
            FoundIndex = PwdPunchIndex;
            await Dispatcher.UIThread.InvokeAsync(PunchRegister);
            PwdPunchIndex = -2;
        }

        private async Task PunchRegister()
        {
            try {
                //ToggleTimers(false); //Detiene los temporizadores para calcular el tiempo exacto hasta finalizar el registro
                DateTime client_time = GetCurrentClockTime();
                TimeSpan run_time = GlobalVars.RunningTime.Elapsed;
                DateTime calc_time = GlobalVars.StartTime.Add(run_time);

                await UpdateInfoLabels(4); //Leyenda procesando
                // Ceder inmediatamente el hilo de UI para que la leyenda se pinte antes de
                // continuar con operaciones subsecuentes potencialmente costosas.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                SearchText = "";

                // Si la huella es valida
                if (FoundIndex > -1)
                {
                    FMDItem? emp_finger = fmd_collection?.ElementAt(FoundIndex); //Busca en el listado de huellas de los colaboradores de acuerdo al indice resultante
                    DataTable dtemployee_punches = await GetEmpPunches(emp_finger.empid);

                    int next_ev = 0;
                    //Si el empleado no tiene registros entonces es una entrada
                    if (dtemployee_punches.Rows.Count <= 0) {
                        next_ev = 1;
                    } else {
                        //Si el empleado tiene registros calcula si es entrada o salida
                        PunchLine last_punch = GetLastPunch(dtemployee_punches);
                        next_ev = await ComputeNextEvent(last_punch, client_time);
                    }

                    //Si el registro esta duplicado
                    if (next_ev == -1)
                    {
                        await PlayBeep(3);
                        await UpdateInfoLabels(2);
                        //ToggleTimers(true); //Reanuda los temporizadores

                        return;
                    }

                    if(next_ev == 0) {
                        await PlayBeep();
                        await UpdateInfoLabels(3);
                        return;
                    }

                    EmpPunches.Clear(); //Limpia el historial de registros del último colaborador

                    await PlayBeep(next_ev);

                    PunchLine new_punch = new() {
                        Punchemp = emp_finger.empid,
                        Punchevent = next_ev,
                        Punchtime = client_time,
                        Punchinternaltime = calc_time
                    };

                    RegisterNewPunch(new_punch, emp_finger);

                    // Muestra los registros de entrada y salida del colaborador actual(DataGridView central)
                    foreach (DataRow dr in dtemployee_punches.Rows)
                    {
                        DateTime dattim = CommonProcs.FromFileString(dr["PuncTime"].ToString());

                        var data = new EmployeePunch() {
                            punchdate = CommonProcs.UpperFirst(dattim.ToString("dddd dd/MM/yyyy")),
                            punchtime = dattim.ToString("HH:mm:ss"),
                            punchevent = CommonObjs.EvTypes[int.Parse(dr["EvID"].ToString() ?? "0")]
                        };

                        EmpPunches.Add(data);
                    }

                    await UpdateInfoLabels(1, emp_finger.empnum, emp_finger.empnom, new_punch);

                    //Agrega el nuevo evento del colaborador
                    EmpPunches.Add(new EmployeePunch() {
                        punchdate = CommonProcs.UpperFirst(new_punch.Punchtime.ToString("dddd dd/MM/yyyy")),
                        punchtime = new_punch.Punchtime.ToString("HH:mm:ss"),
                        punchevent = CommonObjs.EvTypes[new_punch.Punchevent]
                    });

                    //Muestra el evento registrado del colaborador en el DataGridView de la izquierda
                    //OLD: _sourceList.Add(...)
                    var emp_data = new Employee(
                        emp_finger.empid,
                        emp_finger.empnom,
                        new_punch.Punchtime.ToString("dd/MM/yyyy HH:mm"),
                        CommonObjs.EvTypes[new_punch.Punchevent]
                    );

                    if(!IsDuplicated(emp_data)) {
                        AttsList.Add(emp_data);
                    }

                    SelectedIndex = AttsList.Count - 1;
                    SelectedItem = 0;
                    LastEmployeeEventItem = EmpPunches.Count - 1;
                } 
                else {
                    log.Warn("No se encontró la huella dactilar del colaborador.");

                    EmpPunches.Clear();
                    await PlayBeep();
                    await UpdateInfoLabels(3);
                }

                //ToggleTimers(true); //Reanuda el temporizador esperando otro registro de asistencia.
            } catch (Exception exc) {
                var clock = RegAccess.GetClockSettings() ?? new ClockSettings();

                SendBackgroundAlert("Error al procesar la huella (PunchRegister)", exc, new {
                    FoundIndex,
                    SearchText,
                    CurrentTime = GetCurrentClockTime(),
                    DeviceId = clock.clock_uuid
                });

                //ToggleTimers(true);
                await ShowMessage("Error al procesar la huella", exc.Message);
            } finally {
            
            }
        }

        /// <summary>
        /// Obtiene el historial de registros de asistencia del colaborador detectado, en el listado de checadas de todos los empleados en una oficina almacenados en memoria.
        /// </summary>
        /// <param name="emp_id"></param>
        /// <returns></returns>
        private async Task<DataTable> OfflineEmpPunches(int emp_id)
        {
            DateTime el_timestamp;
            DataTable dt = CommonObjs.VoidPunches;

            try {
                await Task.Run(() =>  {
                    foreach (DataRow dr in emp_punches.Rows)
                    {
                        int el_emp = int.Parse(dr["EmpID"].ToString() ?? "0");

                        if (el_emp == emp_id) {
                            el_timestamp = Convert.ToDateTime(dr["PuncTime"].ToString());

                            dt.Rows.Add(el_emp, dr["EvID"], el_timestamp.ToString("yyyyMMddHHmmss"));
                        }
                    }
                });

                DataView dv = new(dt, "", "PuncTime DESC", DataViewRowState.CurrentRows);
                DataTable dt1 = dv.ToTable().AsEnumerable().Take(10).CopyToDataTable();
                DataView dv2 = new(dt1, "", "PuncTime ASC", DataViewRowState.CurrentRows);

                return dv2.ToTable();
            } catch(Exception ex) { log.Error("Error fetching punches by employee: " + ex.Message); }

            return dt;
        }

        /// <summary>
        /// Obtiene el historial de registros de asistencia del colaborador directamente de la BD.
        /// </summary>
        /// <returns></returns>
        private static async Task<DataTable> OnlineEmpPunches(int emp_id)
        {
            DataTable dt = CommonObjs.VoidPunches;

            try {
                DataTable DBEmpPunches = await CommonProcs.GetLastPunchesAsync(new ScantRequest { Question = GlobalVars.clockSettings.clock_office.ToString() }, emp_id);

                foreach (DataRow dr in DBEmpPunches.Rows)
                {
                    DateTime el_timestamp = Convert.ToDateTime(dr["PuncTime"].ToString());
                    dt.Rows.Add(emp_id, dr["EvID"], el_timestamp.ToString("yyyyMMddHHmmss"));
                }
            } catch { }

            return dt;
        }

        /// <summary>
        /// Obtiene el historial de registros de asistencia del colaborador detectado.
        /// </summary>
        /// <param name="emp_id">ID del colaborador.</param>
        /// <returns>DataTable con el histórico de registros del colaborador.</returns>
        private async Task<DataTable> GetEmpPunches(int emp_id, bool be_offline = true)
        {
            DataTable dt = CommonObjs.VoidPunches;
            try {
                dt = be_offline ? await OfflineEmpPunches(emp_id) : await OnlineEmpPunches(emp_id);
            } catch {}

            return dt;
        }

        /// <summary>
        /// Crea un objeto con la información del último registro de asistencia del historial del colaborador detectado.
        /// </summary>
        /// <param name="emp_pncs">DataTable con el histórico de los registros de asistencias del colaborador.</param>
        /// <returns>Objeto PunchLine que encapsula la información del último registro de asistencia encontrado.</returns>
        private static PunchLine GetLastPunch(DataTable emp_pncs)
        {
            DataRow dr = emp_pncs.Rows[^1];

            return new PunchLine {
                Punchemp = int.Parse(dr["EmpID"].ToString() ?? "0"),
                Punchevent = int.Parse(dr["EvID"].ToString() ?? "0"),
                Punchtime = CommonProcs.FromFileString(dr["PuncTime"].ToString())
            };
        }

        /// <summary>
        /// Calcula el siguiente tipo de evento basado en el último evento registrado y los parámetros definidos en la configuración del sistema.
        /// </summary>
        /// <param name="last_punch">Objeto con la información del último registro de asistencia.</param>
        /// <param name="new_punch">Momento del nuevo evento de asistencia.</param>
        /// <returns>El valor del elemento del arreglo EvTypes correspondiente al siguiente evento calculado.</returns>
        private static async Task<int> ComputeNextEvent(PunchLine last_punch, DateTime new_punch)
        {
            if(last_punch == null)
            {
                log.Warn("No se encontró el último evento registrado. Los datos son nulos y se asignará Entrada como el siguiente evento.");
                
                SendBackgroundAlert("Último evento no encontrado (ComputeNextEvent)", new Exception("last_punch es nulo"), new {
                    new_punch,
                    Params = new { DupSeconds = CommonProcs.ParamInt(3), MaxShift = CommonProcs.ParamTSpan(12) }
                });

                return 1; //Si no hay registro previo, entonces es entrada
            }

            TimeSpan ev_diff = new_punch.Subtract(last_punch.Punchtime);
            //Validar si ya cumplió el tiempo estipulado para no considerarlo como duplicado
            if (ev_diff.TotalSeconds < CommonProcs.ParamInt(3)) {
                return -1;
            }

            try {
                switch (last_punch.Punchevent)
                {
                    case 2: //Si es salida
                        //Podría presentarse el caso de que marque "Entrada" cuando sea "Salida" debido a un fallo de sincronización de datos
                        //Normalmente la salida es a las 5:05, entonces validamos si la hora actual es mayor o igual a las 5.
                        int _evt = 1;

                        //if (new_punch.Hour >= 17) {
                        //    _evt = -1;
                        //    await PlayBeep(3);

                        //    var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                        //    var windows = MainWindow?.OwnedWindows;
                        //    if (MainWindow is { } mainWindow && windows.Count == 0) {
                        //        var dialog = new EventPromptWindow() {
                        //            DataContext = new EventPromptViewModel(TimeSpan.Zero)
                        //        };

                        //        Messenger.Send("TogglePanel", true);
                        //        _evt = await dialog.ShowDialog<int>(mainWindow);
                        //        Messenger.Send("TogglePanel", false);
                        //    }
                        //}

                        return _evt; //Entonces es entrada

                    case 1: //Si es entrada
                        TimeSpan max_shift = CommonProcs.ParamTSpan(12);
                        //En caso de que hayas registrado entrada pero no salida. Ya paso más tiempo de lo permitido.

                        if (ev_diff > max_shift) {
                            int evt = -1;

                            var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                            var windows = MainWindow?.OwnedWindows;

                            if (MainWindow is { } mainWindow && windows.Count == 0) {
                                var dialog = new EventPromptWindow() {
                                    DataContext = new EventPromptViewModel(max_shift)
                                };

                                Messenger.Send("TogglePanel", true);
                                evt = await dialog.ShowDialog<int>(mainWindow);
                                Messenger.Send("TogglePanel", false);
                            }

                            return evt;
                        } else {
                            return 2; //Devuelve salida
                        }
                    default:
                        log.Warn("El último evento registrado no es válido. Se asignará Entrada como el siguiente evento al Empleado con ID: " + last_punch?.Punchemp);
                        
                        SendBackgroundAlert("Último evento no válido (ComputeNextEvent)", new Exception("Evento inválido: " + last_punch?.Punchevent), new {
                            last_punch,
                            new_punch,
                            Params = new { DupSeconds = CommonProcs.ParamInt(3), MaxShift = CommonProcs.ParamTSpan(12) }
                        });

                        return 1;
                }
            } catch(Exception ex) {
                log.Error($"Error al calcular siguiente evento: {ex.Message}. Se asignará Entrada como el siguiente evento");

                var clock = RegAccess.GetClockSettings() ?? new ClockSettings();

                SendBackgroundAlert("Error al calcular siguiente evento (ComputeNextEvent)", ex, new {
                    last_punch,
                    new_punch,
                    Params = new { DupSeconds = CommonProcs.ParamInt(3), MaxShift = CommonProcs.ParamTSpan(12) },
                    DeviceId = clock.clock_uuid
                });

                return 1;
            }
        }

        /// <summary>
        /// Almacena el nuevo registro de asistencia en memoria. Además, si el cliente Checador esta conectado al servidor WebSocket, registra en tiempo real el evento indicado en la BD, en caso contrario, lo guarda en caché, en espera de su sincronización.
        /// </summary>
        /// <param name="new_punch">Objeto con la información del nuevo registro de asistencia.</param>
        private void RegisterNewPunch(PunchLine new_punch, FMDItem? emp = null)
        {
            log.Info("El empleado "+ emp.empnom +" ha registrado " + CommonObjs.EvTypes[new_punch.Punchevent] + " a las " + new_punch.Punchtime.ToString("HH:mm:ss") + " horas.");

            emp_punches.Rows.Add(new_punch.Punchemp.ToString(), new_punch.Punchevent.ToString(), new_punch.Punchtime.ToString("yyyy/MM/dd HH:mm:ss"), new_punch.Punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss"));
            GlobalVars.AppCache.StorePunch(new_punch);

            if (IsNetConnected) {
                try
                {
                    Dispatcher.UIThread.InvokeAsync(async () => await SavePunchToDB(new_punch));
                }
                catch (Exception ex)
                {
                    log.Error("Error registering punch: " + ex.Message);
                }

                Dispatcher.UIThread.InvokeAsync(async () => await _realtime!.SendMessageAsync(new_punch, emp));
            }
        }

        /// <summary>
        /// Sends an administrative error alert in the background, either by enqueuing it in a background queue or by
        /// directly notifying the alert service if the queue is unavailable.
        /// </summary>
        /// <remarks>This method attempts to use a background queue to avoid blocking the main execution
        /// flow. If the queue is unavailable, it falls back to a fire-and-forget approach by directly invoking the
        /// alert service asynchronously.</remarks>
        /// <param name="title">The title of the alert, providing a brief description of the error.</param>
        /// <param name="ex">The exception that triggered the alert. This is used to populate the alert details.</param>
        /// <param name="context">An object representing additional context for the alert, which will be serialized into the alert message.</param>
        private static void SendBackgroundAlert(string title, Exception ex, object context) {
            try
            {
                var clock = RegAccess.GetClockSettings() ?? new ClockSettings();
                var alert = new AdminErrorAlert
                {
                    Title = title,
                    Severity = "Error",
                    Message = ex.Message,
                    Exception = ex.ToString(),
                    Context = JsonConvert.SerializeObject(context),
                    OfficeId = clock.clock_office.ToString(),
                    DeviceUUID = clock.clock_uuid ?? string.Empty,
                    Timestamp = DateTime.Now
                };

                // Preferir cola en segundo plano para no bloquear flujo principal
                var queue = App.ServiceProvider.GetService<AdminAlertBackgroundQueue>();
                if (queue != null)
                {
                    queue.Enqueue(alert);
                }
                else
                {
                    // Fallback fire-and-forget directo si no hay cola disponible
                    var alertSvc = App.ServiceProvider.GetService<IAdminAlertService>();
                    _ = Task.Run(async () => await (alertSvc?.NotifyErrorAsync(alert.Title!, ex, alert) ?? Task.CompletedTask));
                }
            }
            catch { }
        }

        private async Task SavePunchToDB(PunchLine new_punch)
        {
            ScantRequest scantreq = new() {
                Question = (
                    (c_settings?.clock_office).ToString() + "|" +
                    new_punch.Punchemp.ToString() + "|" +
                    new_punch.Punchevent + "|" +
                    new_punch.Punchtime.ToString("yyyy/MM/dd HH:mm:ss") + "|" +
                    new_punch.Punchinternaltime.ToString("yyyy/MM/dd HH:mm:ss")
                )
            };

            if (!await CommonProcs.PunchRegisterAsync(scantreq))
            {
               log.Warn("The event of the employee with ID " + new_punch.Punchemp + " could not be registered.");
            }
        }

        /// <summary>
        /// Updates the history panel with the latest attendance records for the current day. All employee punch data is filtered,
        /// </summary>
        /// <remarks>This method filters and processes employee punch data to display attendance records
        /// for the current day. It groups and orders the data, updates the internal collections, and refreshes the UI
        /// elements associated with the history panel.</remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task UpdateHistoryPanel()
        {
            int emp_idx = -1;
            string? emp_name = string.Empty;

            DateTime today = GetCurrentClockTime();

            if(emp_punches.Columns.Count <= 1)
            {
                var error_dt = emp_punches.Rows[0]["ERROR"].ToString() ?? "Error desconocido al recuperar los registros de asistencia.";
                log.Error("Error retrieving punch records: " + error_dt);
                return;
            }

            var filteredRows = emp_punches?.AsEnumerable()
            .Where(p => {
                _ = DateTime.TryParse(p["PuncTime"]?.ToString(), out DateTime pTime);
                return pTime >= new DateTime(today.Year, today.Month, today.Day, 0, 0, 0);
            })
            .GroupBy(row => new {
                EmpID = row["EmpID"]?.ToString() ?? "0",
                EvID = row["EvID"]?.ToString() ?? "0",
                PuncTime = row["PuncTime"]?.ToString() ?? string.Empty
            })
            .Select(group => group.First())
            .OrderBy(x => {
                _ = int.TryParse(x["EmpID"]?.ToString(), out int empId);
                return empId;
            })
            //.Take(40)
            .ToList();

            if (filteredRows != null && filteredRows.Count != 0)
            {
                try {
                    // Copy the filtered rows to a new DataTable and sort by "PuncTime"
                    DataTable dt = new DataView(filteredRows.CopyToDataTable(), "", "PuncTime ASC", DataViewRowState.CurrentRows).ToTable();

                    AttsList.Clear();
                    EmpPunches.Clear();
                    Employee.ResetAndReloadData();

                    foreach (DataRow dr in dt.Rows) {
                        _ = int.TryParse(dr["EmpID"]?.ToString(), out int empId);
                        emp_idx = fmd_collection.FindIndex(e => e.empid == empId);
                        emp_name = (emp_idx > -1) ? fmd_collection.ElementAt(emp_idx)?.empnom : string.Empty;

                        if (!string.IsNullOrWhiteSpace(emp_name)) {
                            //DateTime PuncTime = CommonProcs.FromFileString(dr["PuncTime"]?.ToString() ?? string.Empty);
                            _ = DateTime.TryParse(dr["PuncTime"].ToString(), out DateTime PuncTime);
                            _ = int.TryParse(dr["EvID"]?.ToString(), out int evId);

                            //OLD: _sourceList.Add(...)
                            AttsList.Add(new Employee(
                               empId,
                               emp_name,
                               PuncTime.ToString("dd/MM/yyyy HH:mm"),
                               CommonObjs.EvTypes[evId]
                            ));
                        }
                    }

                    SelectedIndex = AttsList.Count - 1;//_sourceList.Count - 1;
                    SelectedItem = 0;
                } catch (Exception ex) {
                    log.Error(new Exception(), "Error al llenar el panel del histórico de registros de asistencia: " + ex);
                }
            }

            await UpdateInfoLabels(5);
        }

        /// <summary>
        /// Consolida los registros de asistencia almacenados en caché con los recuperados del histórico, para poblar el panel histórico de la aplicación.
        /// </summary>
        private async Task ConsolidateHistory()
        {
            await GetRecentPunches();
            string[] cached_punches = GlobalVars.AppCache.GetCachedPunches(2);
            string[] punch_parts;

            foreach (string str in cached_punches)
            {
                try {
                    punch_parts = str.Split(['|']);

                    int EmpID = int.Parse(punch_parts[0]);
                    int EvID = int.Parse(punch_parts[1]);
                    string? PuncTime = (punch_parts.Length > 2) ? CommonProcs.ParseValidDT(punch_parts[2], "yyyy/MM/dd HH:mm:ss") : null;
                    string? PuncCalc = (punch_parts.Length > 3) ? CommonProcs.ParseValidDT(punch_parts[3], "yyyy/MM/dd HH:mm:ss") : null;

                    //emp_punches.Rows.Add(EmpID, EvID, DateTime.Parse(punch_parts[2]), PuncCalc);
                    // Check emp_punches has ERROR column
                    if (emp_punches.Columns.Contains("ERROR")) {
                        emp_punches.Columns.Remove("ERROR");
                    }

                    emp_punches.Rows.Add(EmpID, EvID, PuncTime, PuncCalc);
                } catch (Exception ex) {
                    log.Error("Error parsing cached punch: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Valida si el registro de asistencia ya existe en la lista de asistencias. Si no existe, lo agrega a la lista y lo almacena en caché.
        /// </summary>
        /// <param name="message"></param>
        public async void ValidateReceivedPunch(string message)
        {
            Employee? EmpData = Employee.FromJson(message);

            // Verifica si estos en el hilo de la UI
            if (Dispatcher.UIThread.CheckAccess())
            {
                // Si ya estás en el hilo de la UI, simplemente agrega el elemento
                if (!IsDuplicated(EmpData))
                {
                    AttsList.Add(EmpData!);
                    await AddEmployee(message);
                }
            }
            else
            {
                // Si no estás en el hilo de la UI, usa Dispatcher para invocar la operación en el hilo de la UI
                if (!IsDuplicated(EmpData))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => AttsList.Add(EmpData!));
                    await AddEmployee(message);
                }
            }

            SelectedIndex = AttsList.Count - 1;
        }

        /// <summary>
        /// Agrega un registro de Evento recibido por medio de un Canal de Websocket, en memoria y cache para sincronizar diferentes dispositivos conectados a un canal de WebSocket.
        /// </summary>
        /// <param name="PunchRecord"></param>
        /// <returns></returns>
        public async Task<bool> AddEmployee(string message)
        {
            PunchRecord? PunchRecord = JsonConvert.DeserializeObject<PunchRecord>(message);

            PunchLine new_punch = new()
            {
                Punchemp = PunchRecord.IdEmployee,
                Punchevent = PunchRecord.IdEvent,
                Punchtime = PunchRecord?.EventTime != null ? DateTime.Parse(PunchRecord.EventTime) : DateTime.MinValue,
                Punchinternaltime = PunchRecord?.InternalEventTime != null ? DateTime.Parse(PunchRecord.InternalEventTime) : DateTime.MinValue
            };

            // Agrega el registro en la lista de la "memoria"
            emp_punches.Rows.Add(new_punch.Punchemp.ToString(), new_punch.Punchevent.ToString(), PunchRecord.EventTime, PunchRecord.InternalEventTime);

            // Agrega el registro en el archivo de Cache
            GlobalVars.AppCache.StorePunch(new_punch);

            return await Task.FromResult(true);
        }
    }
}