using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using VTACheckClock.Models;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.ViewModels
{
    class PwdPunchViewModel : ViewModelBase
    {
        private string? _usr = "", _pwd = "";
        private int _foundIndex = -1;
        private readonly List<FMDItem>? FMDCollection;

        public PwdPunchViewModel(List<FMDItem>? fmd_collection)
        {
            FMDCollection = fmd_collection;
            IObservable<bool>? okEnabled = this.WhenAnyValue(
                x => x.Username, x => x.Password, 
                (user, pass) =>
                    !string.IsNullOrWhiteSpace(user) && user.Length >= 0 &&
                    !string.IsNullOrWhiteSpace(pass) && pass.Length >= 5 && pass.Length <= 15
                )
                .DistinctUntilChanged();

            PwdPunchCommand = ReactiveCommand.CreateFromTask(Login, okEnabled);

            CancelCommand = ReactiveCommand.Create(() => { });
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

        public int FoundIndex
        {
            get => _foundIndex;
            set => this.RaiseAndSetIfChanged(ref _foundIndex, value);
        }

        public ReactiveCommand<Unit, int> PwdPunchCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public async Task<int> Login()
        {
            _ = int.TryParse(Username, out int emp_id);
            int found_idx = FMDCollection.FindIndex(f => (f.empid == emp_id) && (f.emppass.Equals(Password?.ToLower(), StringComparison.CurrentCultureIgnoreCase)));
            if(found_idx == -1) {
                await ShowMessage("Claves incorrectas.", "El número de colaborador y/o la contraseña introducida son incorrectos. Por favor, rectifique e intente de nuevo.");
            }

            return found_idx;
        }

    }
}
