using Dynamologio.Core.Models;

namespace Dynamologio.App.Services
{
    public interface IPersonEditorDialogService
    {
        bool ShowAddDialog();
        bool ShowEditDialog(Personnel person);
    }
}
