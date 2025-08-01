using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using VTACheckClock.Helpers;
using VTACheckClock.ViewModels;

namespace VTACheckClock.Views
{
    /*public*/ partial class ConfigurationWindow : ReactiveWindow<ConfigurationViewModel> /*Window*/
    {
        public ConfigurationWindow()
        {
            InitializeComponent();
            this.FindControl<Button>("btnCancel").Click += delegate {
                Close();
                Messenger.Send("TogglePanel", false);
            };
            this.WhenActivated(d => d(ViewModel!.ShowDBDialog.RegisterHandler(DoShowDialogAsync)));
            this.WhenActivated(d => d(ViewModel!.ShowLoginDialog.RegisterHandler(DoShowDialogAsync)));

            PropertyChanged += ConfigurationWindow_PropertyChanged;
        }

        private void ConfigurationWindow_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == HeightProperty) {
                WindowHelper.CenterOnScreen(this);
            }

            if (e.Property == WidthProperty)
            {
                WindowHelper.CenterOnScreen(this);
            }
        }

        private async Task DoShowDialogAsync(IInteractionContext<DatabaseConnectionViewModel, bool> interaction)
        {
            var dialog = new DatabaseConnectionWindow {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
            ((ConfigurationViewModel?)DataContext).IsConfigured = result;
            if (result) ((ConfigurationViewModel?)DataContext).NextStep = 2;
        }

        private async Task DoShowDialogAsync(IInteractionContext<LoginViewModel, bool> interaction)
        {
            var dialog = new LoginWindow {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        }
    }
}
