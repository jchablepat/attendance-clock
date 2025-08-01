using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VTACheckClock.Helpers;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;
using VTACheckClock.Views;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.ViewModels
{
    class LoginViewModel : ViewModelBase
    {
        private string? _usr = "", _pwd = "";
        private bool _canAccess;

        public LoginViewModel()
        {
            IObservable<bool>? okEnabled = this.WhenAnyValue(
                x => x.Username, x => x.Password, 
                (user, pass) =>
                    !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass) //&&
                    //user.Length >= 3 && pass.Length >= 8
                )
                .DistinctUntilChanged();

            OkCommand = ReactiveCommand.CreateFromTask(Login, okEnabled);

            CancelCommand = ReactiveCommand.Create(() => {});

            //ShowSettingsDialog = new Interaction<ClockSettingsViewModel, bool>();
        }

        public string? Username
        {
            get => _usr;
            set => this.RaiseAndSetIfChanged(ref _usr, value);
        }

        public string? Password
        {
            get => _pwd;
            set => this.RaiseAndSetIfChanged(ref _pwd, value);
        }

        public bool CanAccess
        {
            get => _canAccess;
            set => this.RaiseAndSetIfChanged(ref _canAccess, value);
        }

        public ReactiveCommand<Unit, bool> OkCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public Interaction<ClockSettingsViewModel, bool> ShowSettingsDialog { get; } = new();
        public ICommand? ClockSettingsCommand { get; set; }

        public async Task<bool> Login()
        {
            SessionData my_login = new() {
                usrname = Username,
                passwd = Password,
                accpriv = GlobalVars.ClockStartPriv
            };

            CanAccess = CommonValids.ValidaLogin(my_login);
            if (!CanAccess)
            {
                await ShowMessage("Credenciales incorrectas", "El usuario y/o la contraseña no son válidos.", 300);
            }

            return CanAccess;
        }
    }
}
