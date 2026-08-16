using System.Windows;
using Dynamologio.App.ViewModels;

namespace Dynamologio.App.Views
{
    public partial class PersonEditorDialog : Window
    {
        public PersonEditorDialog(PersonEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = () =>
            {
                DialogResult = viewModel.DialogResult;
                Close();
            };
        }
    }
}
