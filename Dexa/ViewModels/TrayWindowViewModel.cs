
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dexa.Models;
using Dexa.Views;

namespace Dexa.ViewModels
{
    public partial class TrayWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<SampleItem> _sampleItems;

        public TrayWindowViewModel()
        {
            SampleItems = new ObservableCollection<SampleItem>
            {
                new SampleItem { DisplayName = "Element 1" },
                new SampleItem { DisplayName = "Element 2" },
                new SampleItem { DisplayName = "Element 3" }
            };
        }

        [RelayCommand]
        private void ShowHelp()
        {
            var helpWindow = new HelpWindow
            {
                DataContext = new HelpWindowViewModel()
            };
            helpWindow.Show();
        }

        [RelayCommand]
        private void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
