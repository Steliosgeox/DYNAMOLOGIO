using System;
using System.Windows;

namespace Dynamologio.App.Services
{
    public class WpfConfirmationService : IConfirmationService
    {
        public bool Confirm(string title, string message)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }
    }
}
