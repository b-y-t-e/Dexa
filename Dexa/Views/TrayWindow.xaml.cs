
using System;
using System.Windows;

namespace Dexa.Views
{
    public partial class TrayWindow : Window
    {
        public TrayWindow()
        {
            InitializeComponent();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
