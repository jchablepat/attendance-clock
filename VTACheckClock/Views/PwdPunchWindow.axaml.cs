using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using VTACheckClock.Helpers;
using VTACheckClock.Services;
using VTACheckClock.ViewModels;

namespace VTACheckClock.Views
{
    /*public*/ partial class PwdPunchWindow : ReactiveWindow<PwdPunchViewModel> /*Window*/
    {
        private bool ForceClose = false;
        public PwdPunchWindow()
        {
            InitializeComponent();
            this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(model =>
            {
                ForceClose = true;
                Close(-1); //Just to indicate has closed set -1
            })));

            this.WhenActivated(d => d(ViewModel!.PwdPunchCommand.Subscribe(_foundIndex =>
            {
                if (_foundIndex != -1) {
                    ForceClose = true;
                    Close(_foundIndex);
                }
            })));

            txtEmpID.KeyDown += OnTextInput;
            Activated += (sender, e) => {
                txtEmpID.Focus();
            };

            WindowHelper.CenterOnScreen(this);
        }

        private void OnWindowClosing(object sender, WindowClosingEventArgs e) {
            e.Cancel = true;
            if (!ForceClose) {
                ForceClose = true;
                Close(-1);
            } else {
                e.Cancel = false;
            }

        }

        private void OnTextInput(object? sender, KeyEventArgs e)
        {
            // Verificar si la tecla presionada es numérica
            if (!IsNumericKey(e.Key)) {
                // Si la tecla presionada no es numérica, cancelar el evento
                e.Handled = true;
            }
        }

        private bool IsNumericKey(Key key) {
            // Verificar si la tecla presionada es numérica
            return key >= Key.D0 && key <= Key.D9 || key >= Key.NumPad0 && key <= Key.NumPad9;
        }

        private void TriggerEnter_OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(txtEmpID.Text) && !string.IsNullOrEmpty(txtEmpPass.Text))
            {
                btnLogin.Command?.Execute(null);
            }
        }
    }
}
