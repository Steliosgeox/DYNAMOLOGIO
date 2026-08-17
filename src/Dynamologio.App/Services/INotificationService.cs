using System;

namespace Dynamologio.App.Services
{
    public interface INotificationService
    {
        void Info(string title, string message);
        void Warning(string title, string message);
        void Error(string title, string message);
    }
}
