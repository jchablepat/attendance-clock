using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MsBox.Avalonia;
using VTACheckClock.Helpers;
using VTACheckClock.Views;

namespace VTACheckClock;

public partial class EmployeeCheckWindow : Window
{
    public string? SelectedAction { get; private set; }
    public int EmployeeId { get; private set; }

    public EmployeeCheckWindow()
    {
        InitializeComponent();
        EntryButton.Click += (_, _) => { CloseDialog("Entrada"); };
        ExitButton.Click += (_, _) => { CloseDialog("Salida"); };
        
        WindowHelper.CenterOnScreen(this);
        
        Activated += (sender, e) => {
            txtEmployeeId.Focus();
        };
    }

    private void CloseDialog(string action)
    {
        if (int.TryParse(txtEmployeeId.Text, out int empId)) {
            EmployeeId = empId;
            SelectedAction = action;
            Close();
        }
        else
        {
            MessageBoxManager
                .GetMessageBoxStandard("Error", "Ingrese un ID válido.", MsBox.Avalonia.Enums.ButtonEnum.Ok)
                .ShowWindowDialogAsync(this);
        }
    }
}