using Avalonia.Controls;

namespace VTACheckClock.Views
{
    public partial class EventPromptWindow : Window
    {
        public EventPromptWindow()
        {
            InitializeComponent();
            btnExit.Click += delegate {
                this.Close(2);
            };

            btnEnter.Click += delegate {
                this.Close(1);
            };
        }
    }
}
