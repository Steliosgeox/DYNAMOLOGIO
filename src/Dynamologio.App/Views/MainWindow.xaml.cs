using System.Windows;
using Dynamologio.App.ViewModels;

namespace Dynamologio.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
