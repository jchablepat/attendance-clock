using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using VTACheckClock.ViewModels;

namespace VTACheckClock.Views
{
    public partial class PunchSyncPreviewWindow : ReactiveWindow<PunchSyncPreviewViewModel>
    {
        public PunchSyncPreviewWindow()
        {
            InitializeComponent();

            this.WhenActivated(d => d(ViewModel!.SyncNowCommand.Subscribe(result =>
            {
                Close(result);
            })));

            this.WhenActivated(d => d(ViewModel!.CancelCommand.Subscribe(result =>
            {
                Close(result);
            })));
        }
    }
}