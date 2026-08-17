using System;

namespace Dynamologio.App.Services
{
    public interface IPrintService
    {
        bool PrintDocument(object document, string title);
    }
}
