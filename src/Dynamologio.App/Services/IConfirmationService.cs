using System;

namespace Dynamologio.App.Services
{
    public interface IConfirmationService
    {
        bool Confirm(string title, string message);
    }
}
