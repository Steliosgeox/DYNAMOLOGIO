using System;

namespace Dynamologio.App.Services
{
    public interface IFileDialogService
    {
        string OpenExcelFile();
        string SaveExcelFile(string defaultName);
        string SelectBackupFile();
        string SelectBackupDestination(string defaultName);
    }
}
