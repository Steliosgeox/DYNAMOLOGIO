using System;
using System.IO;
using Microsoft.Win32;

namespace Dynamologio.App.Services
{
    public class WpfFileDialogService : IFileDialogService
    {
        public string OpenExcelFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
                Title = "Επιλογή Αρχείου Excel"
            };

            if (ofd.ShowDialog() == true)
            {
                return ofd.FileName;
            }
            return null;
        }

        public string SaveExcelFile(string defaultName)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = defaultName
            };

            if (sfd.ShowDialog() == true)
            {
                return sfd.FileName;
            }
            return null;
        }

        public string SelectBackupFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Dynamologio Backup (*.zip)|*.zip",
                Title = "Επιλογή Αντιγράφου Ασφαλείας προς Επαναφορά"
            };

            if (ofd.ShowDialog() == true)
            {
                return ofd.FileName;
            }
            return null;
        }

        public string SelectBackupDestination(string defaultName)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Dynamologio Backup (*.zip)|*.zip",
                FileName = defaultName
            };

            if (sfd.ShowDialog() == true)
            {
                return sfd.FileName;
            }
            return null;
        }
    }
}
