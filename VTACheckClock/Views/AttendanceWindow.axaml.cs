using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Input;
using VTACheckClock.DBAccess;
using VTACheckClock.Models;
using VTACheckClock.ViewModels;
using System.Threading.Tasks;
using VTACheckClock.Services;
using NLog;
using static VTACheckClock.Views.MessageBox;
using ClosedXML.Excel;
using System.IO;

namespace VTACheckClock.Views
{
    partial class AttendanceWindow : ReactiveWindow<AttendanceViewModel>
    {
        public ObservableCollection<AttendanceRecord> Attendances { get; set; }
        private readonly Logger log = LogManager.GetLogger("app_logger");

        public AttendanceWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(model => { Close(); })));

            Attendances = new ObservableCollection<AttendanceRecord>();

            this.MinWidth = 1000;
            this.MinHeight = 500;
        }

        public async void GenerateReport(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.IsVisible = true;
            BtnGenerateReport.IsEnabled = false;

            try {
                var startDate = StartDatePicker.SelectedDate;
                var endDate = EndDatePicker.SelectedDate;
                var office = CmbOff.SelectedValue as OfficeData;

                // Primero obtener los datos
                var result = await DBMethods.GetAttendanceByDateRange(office.Offid, startDate!.Value.DateTime, endDate!.Value.DateTime);

                // Limpiar datos existentes
                Attendances.Clear();

                // Crear un nuevo DataGrid
                var newAttendanceGrid = new DataGrid {
                    Name = "AttendanceGrid",
                    AutoGenerateColumns = false,
                    FrozenColumnCount = 3,
                    Classes = { "dgAttendanceRpt" }
                };

                // Agregar columnas fijas
                AddFixedColumns(newAttendanceGrid);

                // Generar columnas dinámicas para fechas
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    var dateKey = currentDate.Value.ToString("yyyyMMdd");

                    AddColumnForDate(newAttendanceGrid, dateKey, currentDate.Value.ToString("dd MMM yyyy"));

                    currentDate = currentDate.Value.AddDays(1);
                }

                foreach (DataRow dr in result.Rows)
                {
                    Attendances.Add(new AttendanceRecord {
                        EmployeeId = Convert.ToInt32(dr["EmpID"].ToString()),
                        EmployeeCode = dr["EmpCode"].ToString()!,
                        EmployeeName = dr["EmployeeName"].ToString()!,
                        EventId = Convert.ToInt32(dr["EventId"].ToString()),
                        EventType = dr["Evento"].ToString()!,
                        DailyStatus = GenerateDailyStatus(startDate.Value, endDate.Value, dr)
                    });
                }

                // Establecer origen de datos
                newAttendanceGrid.ItemsSource = Attendances;

                // Asumiendo que tienes un contenedor que contiene el DataGrid original
                DataGridContainer.Children.Clear();
                DataGridContainer.Children.Add(newAttendanceGrid);
            }
            catch (Exception exception)
            {
                await ShowMessage("Operación inválida", "Error al generar el reporte de Asistencias: " + exception.Message, 450);

                log.Warn("Error al generar el reporte de Asistencias: "+ exception.Message);
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
                BtnGenerateReport.IsEnabled = true;
            }
        }

        private void AddFixedColumns(DataGrid attendanceGrid)
        {
            attendanceGrid.Columns.Add(new DataGridTextColumn {
                Header = new StackPanel {
                    Children = {
                        new TextBlock { Text = "", Height = 25 },
                        new TextBlock { Text = "" }
                    }
                },
                Binding = new Binding(nameof(AttendanceRecord.EmployeeCode)),
                IsReadOnly = true
            });

            attendanceGrid.Columns.Add(new DataGridTextColumn {
                Header = 
                    //new StackPanel {
                    //Children = {
                        //new TextBlock { Text = "Empleado", Height = 25 },
                        CreateEmployeeNameFilter(attendanceGrid),
                    //},
                    
                //},
                Binding = new Binding(nameof(AttendanceRecord.EmployeeName)),
                IsReadOnly = true
            });

            attendanceGrid.Columns.Add(new DataGridTextColumn {
                Header = new StackPanel {
                    Children = {
                        new TextBlock { Text = "Evento", Height = 25 },
                        new TextBlock { Text = "" }
                    }
                },
                Binding = new Binding(nameof(AttendanceRecord.EventType)),
                IsReadOnly = true
            });
        }

        private Control CreateEmployeeNameFilter(DataGrid attendanceGrid)
        {
            var filterTextBox = new TextBox() {
                Watermark = "Filtrar empleado...",
                Width = 340,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 6),
            };

            // Agregar evento de filtrado
            filterTextBox.TextChanged += (sender, e) => {
                FilterEmployee(attendanceGrid, filterTextBox.Text!);
            };

            return filterTextBox;
        }

        private void FilterEmployee(DataGrid attendanceGrid, string filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro)) {
                // Si no hay filtro, mostrar todos los registros
                attendanceGrid.ItemsSource = Attendances;
                return;
            }

            // Filtrar la colección basándose en el nombre
            var filteredList = Attendances
                .Where(record =>
                    record.EmployeeName.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Actualizar el origen de datos del DataGrid
            attendanceGrid.ItemsSource = filteredList;
        }

        private void AddColumnForDate(DataGrid attendanceGrid, string dateKey, string headerText)
        {
            // Parsear la fecha para obtener el día de la semana
            var date = DateTime.ParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture);
            var dayName = date.ToString("dddd", new CultureInfo("es-ES")); // Para nombres en español
            dayName = char.ToUpper(dayName[0]) + dayName.Substring(1).ToLower();

            var headerPanel = new StackPanel {
                Children = {
                    new TextBlock {
                        Text = headerText,
                        Height = 25,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock {
                        Text = dayName,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Classes = {
                            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                                ? "Weekend"
                                : ""
                        }
                    }
                }
            };

            var column = new DataGridTemplateColumn {
                Tag = dateKey,
                Header = headerPanel,
                CellTemplate = new FuncDataTemplate<AttendanceRecord>((record, parent) => {
                    // Verificar si la clave existe en el diccionario
                    if (!record.DailyStatus.TryGetValue(dateKey, out _)) {
                        return new TextBlock {
                            Text = "N/A",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                    }

                    var attendanceTimeInfo = record.DailyStatus[dateKey];

                    // Si es vacaciones o descanso, mantenemos el texto sin edición
                    if (attendanceTimeInfo.IsNonWorkingDate) {
                        return new TextBlock {
                            Text = attendanceTimeInfo.BenefitAlias,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = Brushes.Coral,
                            FontWeight = FontWeight.Bold,
                            //Background = new SolidColorBrush(Colors.Coral),
                            Padding = new Thickness(5),
                            Cursor = new Cursor(cursorType: StandardCursorType.No)
                        };
                    }

                    // Para valores de hora, permitimos edición con TimePicker
                    //var timePicker = new TimePicker {
                    //    VerticalAlignment = VerticalAlignment.Center,
                    //    [!TimePicker.SelectedTimeProperty] = new Binding($"DailyStatus[{dateKey}]")
                    //};

                    //return timePicker;

                    var textBox = new TextBlock {
                        Text = attendanceTimeInfo.Event,
                        Classes = {
                            attendanceTimeInfo.Event == "?"
                                ? "NoCheck"
                                : ""
                        },
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Cursor = new Cursor(cursorType: StandardCursorType.Hand)
                    };

                    if(attendanceTimeInfo.Event == "?") {
                        textBox.Foreground = Brushes.DarkRed;
                        textBox.FontWeight = FontWeight.Bold;
                    }

                    return textBox;
                }),
                // Agregar el CellEditingTemplate si es necesario
                CellEditingTemplate = new FuncDataTemplate<AttendanceRecord>((record, parent) => {
                    if (!record.DailyStatus.TryGetValue(dateKey, out _)) {
                        return new TextBlock {
                            Text = "N/A",
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                    }

                    var attendanceTimeInfo = record.DailyStatus[dateKey];

                    if (attendanceTimeInfo.IsNonWorkingDate) {
                        return new TextBlock {
                            Text = attendanceTimeInfo.BenefitAlias,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = Brushes.Coral,
                            FontWeight = FontWeight.Bold,
                            Cursor = new Cursor(cursorType: StandardCursorType.No)
                        };
                    }

                    var timePicker = new TimePicker {
                        VerticalAlignment = VerticalAlignment.Center,
                        [!TimePicker.SelectedTimeProperty] = new Binding($"DailyStatus[{dateKey}].EventTime"),
                        Tag = dateKey,
                        Cursor = new Cursor(cursorType: StandardCursorType.Hand)
                    };

                    timePicker.SelectedTimeChanged += TimePicker_SelectedTimeChanged;

                    return timePicker;
                })
            };

            // Asignar clase de estilo
            //if(date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) {
            //    column.CellStyleClasses.Add("Weekend");
            //}
            
            attendanceGrid.Columns.Add(column);
        }

        private async void TimePicker_SelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs args)
        {
            var office = CmbOff.SelectedValue as OfficeData;

            if (sender is TimePicker timePicker && args.OldTime.HasValue && args.NewTime.HasValue && args.OldTime.Value != args.NewTime.Value)
            {
                // Comparar horas y minutos, ignorando segundos
                if (args.OldTime.Value.Hours != args.NewTime!.Value.Hours || args.OldTime.Value.Minutes != args.NewTime.Value.Minutes)
                {
                    //Debug.WriteLine($"Tiempo cambiado de {args.OldTime.Value} a {args.NewTime.Value}");

                    // Mostrar loading
                    LoadingOverlay.IsVisible = true;
                    timePicker.IsEnabled = false;

                    try {
                        var dateKey = timePicker.Tag as string;

                        if (timePicker.DataContext is AttendanceRecord record && !string.IsNullOrEmpty(dateKey)) {
                            var attendanceTimeInfo = record.DailyStatus[dateKey];
                            var splitTime = attendanceTimeInfo.Event.Split(':');

                            var selectedTime = args.NewTime.Value;
                            var seconds = (splitTime.Length >= 3 ? splitTime[2] : "00");
                            var formattedTime = selectedTime.ToString(@"hh\:mm") + ":" + seconds;
                            attendanceTimeInfo.Event = formattedTime;

                            // Convertir dateKey a DateTime
                            var date = DateTime.ParseExact(dateKey, "yyyyMMdd", CultureInfo.InvariantCulture);
                            
                            // Combinar la fecha existente con la nueva hora
                            var combinedDateTime = date.Date + selectedTime.Add(TimeSpan.FromSeconds(Convert.ToDouble(seconds)));

                            await DBMethods.SaveAttendanceTime(record.EmployeeId, office.Offid, record.EventId, combinedDateTime);
          
                            // Actualizar el modelo
                            //record.DailyStatus[dateKey] = e.NewTime.Value;

                            // Opcional: Mostrar mensaje de éxito
                            await Dispatcher.UIThread.InvokeAsync(() => {
                                // Puedes mostrar una notificación o cambiar el color brevemente
                                timePicker.Background = new SolidColorBrush(Colors.LightGreen);
                                Task.Delay(1000).ContinueWith(_ => {
                                    Dispatcher.UIThread.InvokeAsync(() => {
                                        timePicker.Background = new SolidColorBrush(Colors.Transparent);
                                    });
                                });
                            });
                        } else {
                            await ShowMessage("Operación inválida", "Se detectó un error al tratar de Actualizar el horario.", 350);
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowMessage("Operación incompleta", "Error al actualizar el horario: " + ex.Message, 350);

                        // Manejar errores
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            // Revertir el tiempo si hay error
                            timePicker.SelectedTime = args.OldTime.Value;
                        });
                    }
                    finally
                    {
                        // Ocultar loading
                        await Dispatcher.UIThread.InvokeAsync(() => {
                            LoadingOverlay.IsVisible = false;
                            timePicker.IsEnabled = true;
                        });
                    }
                }
            }
        }

        private Dictionary<string, AttendanceTimeInfo> GenerateDailyStatus(DateTimeOffset start, DateTimeOffset end, DataRow dr)
        {
            var status = new Dictionary<string, AttendanceTimeInfo>();
            var currentDate = start;

            while (currentDate <= end)
            {
                string? value = "N/A";
                var dateKey = currentDate.ToString("yyyyMMdd");
                if(dr.Table.Columns.Contains(dateKey)) {
                    value = dr[dateKey].ToString() ?? "";
                }
                
                var eventTime = dr.Table.Columns.Contains(dateKey)
                    ? IsNonWorkingDay(value)
                        ? TimeSpan.Zero
                        : !string.IsNullOrEmpty(value)
                            ? TimeSpan.Parse(value)
                            : TimeSpan.Zero
                    : TimeSpan.Zero;

                status[dateKey] = new AttendanceTimeInfo() {
                    IsNonWorkingDate = IsNonWorkingDay(value),
                    BenefitAlias = IsNonWorkingDay(value) ? value : "",
                    Event = IsNonWorkingDay(value) ? "" : string.IsNullOrEmpty(value) ? "?": value,
                    EventTime = eventTime
                };

                currentDate = currentDate.AddDays(1);
            }

            return status;
        }

        private static readonly HashSet<string> NonWorkingDayCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "D", "D2", "P", "LF", "F", "I", "V", "N/A"
        };

        private static bool IsNonWorkingDay(string value)
        {
            return NonWorkingDayCodes.Any(code => value.Contains(code, StringComparison.OrdinalIgnoreCase));
        }

        public async Task ExportDataGridToExcel(DataGrid? dataGrid)
        {
            try {
                if (dataGrid == null || dataGrid.ItemsSource is not IEnumerable<AttendanceRecord> itemsSource) {
                    throw new InvalidOperationException("No hay datos en el DataGrid.");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("DataGridExport");
                // Estilos de encabezado
                var headerStyle = workbook.Style;
                headerStyle.Font.Bold = true;
                headerStyle.Fill.BackgroundColor = XLColor.LightBlue;
                headerStyle.Font.FontColor = XLColor.White;
                headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Escribe las columnas (encabezados)
                var columns = dataGrid.Columns;
                int colIndex = 1;

                foreach (var column in columns) {
                    var headerContent = column.Header;
                    string headerText = GetHeaderText(headerContent);
                    var headerCell = worksheet.Cell(1, colIndex++);
                    headerCell.Value = headerText ?? "Sin nombre";
                    headerCell.Style = headerStyle;
                }

                // Escribe las filas
                int rowIndex = 2;

                foreach (var record in itemsSource) {
                    colIndex = 1;

                    foreach (var column in columns)
                    {
                        if (column is DataGridTemplateColumn templateColumn)
                        {
                            // Extrae datos dinámicos del diccionario
                            var headerKey = GetHeaderKey(templateColumn.Tag); // Extraer clave dinámica
                            if (record.DailyStatus.TryGetValue(headerKey, out var attendanceTimeInfo))
                            {
                                var cell = worksheet.Cell(rowIndex, colIndex);
                                cell.Value = string.IsNullOrEmpty(attendanceTimeInfo?.Event) ? attendanceTimeInfo?.BenefitAlias: attendanceTimeInfo?.Event;
                                // Aplicar estilos dinámicos basados en el contenido
                                if (attendanceTimeInfo?.Event == "?" || !string.IsNullOrEmpty(attendanceTimeInfo?.BenefitAlias)) {
                                    cell.Style.Font.FontColor = XLColor.Red;
                                    cell.Style.Font.Bold = true;
                                }
                            } else {
                                worksheet.Cell(rowIndex, colIndex).Value = "N/A";
                            }
                        }
                        else if (column is DataGridTextColumn textColumn) {
                            // Si es una columna manual, obtén el Path del Binding
                            if (textColumn.Binding is Binding binding) {
                                var columnBindingPath = binding.Path;
                                var value = record.GetType().GetProperty(columnBindingPath)?.GetValue(record);

                                worksheet.Cell(rowIndex, colIndex).Value = value?.ToString();
                            }
                        } else {
                            // Maneja otras columnas
                            worksheet.Cell(rowIndex, colIndex).Value = "Sin soporte";
                        }

                        colIndex++;
                    }

                    rowIndex++;
                }

                // Ajusta automáticamente el ancho de las columnas al contenido
                worksheet.Columns().AdjustToContents();

                // Guarda el archivo
                var filePath = await FolderPickerService.OpenFolderBrowser() + @"\\DataGridExport.xlsx";

                await ShowMessage("Exportación finalizada", "Las asistencias se han exportado en Excel en la ruta: \n\n" + filePath, 450);

                workbook.SaveAs(filePath);
            }
            catch (Exception e) {
                await ShowMessage("Operación inválida", "Error al exportar el Excel: " + e.Message, 350);

                log.Warn("Error al exportar el Excel de Asistencias de empleados: " + e.Message);
            }
        }

        /// <summary>
        /// Método para extraer texto del Header
        /// </summary>
        /// <param name="headerContent"></param>
        /// <returns>String Nombre dinámico del header como formato de fecha 'yyyyMMdd'</returns>
        private static string GetHeaderText(object headerContent)
        {
            if (headerContent is string headerString) {
                return headerString;
            }
            else if (headerContent is StackPanel stackPanel) {
                // Busca un TextBlock dentro del StackPanel
                var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();
                
                return textBlock?.Text!;
            }
            else if(headerContent is TextBox textBox) {
                return "";
            }

            return headerContent?.ToString()!;
        }

        /// <summary>
        /// Método para obtener la clave dinámica del Header
        /// </summary>
        /// <param name="headerContent"></param>
        /// <returns></returns>
        private static string GetHeaderKey(object headerContent) {
            // Si usas claves específicas, conviértelo a la representación esperada
            return GetHeaderText(headerContent);
        }

        private async void BtnExportExcel_OnClick(object? sender, RoutedEventArgs e)
        {
            var newAttendanceGrid = DataGridContainer.Children.OfType<DataGrid>().FirstOrDefault();

            await ExportDataGridToExcel(newAttendanceGrid);
        }
    }
}