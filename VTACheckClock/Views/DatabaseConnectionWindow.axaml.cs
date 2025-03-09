using Avalonia.Controls;
using VTACheckClock.Helpers;

namespace VTACheckClock.Views
{
    public partial class DatabaseConnectionWindow : Window
    {
        public DatabaseConnectionWindow()
        {
            InitializeComponent();
            btnCancel.Click += delegate {
                Close();
            };

            WindowHelper.CenterOnScreen(this);
        }
    }
}
