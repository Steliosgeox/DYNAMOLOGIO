using Dynamologio.App.ViewModels;
using Dynamologio.App.Views;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;
using Dynamologio.Infrastructure.Services;

namespace Dynamologio.App.Services
{
    public class WpfPersonEditorDialogService : IPersonEditorDialogService
    {
        private readonly IUnitOfWork _uow;
        private readonly IConflictEngine _conflictEngine;
        private readonly IPersonnelService _personnelService;

        public WpfPersonEditorDialogService(
            IUnitOfWork uow,
            IConflictEngine conflictEngine,
            IPersonnelService personnelService)
        {
            _uow = uow;
            _conflictEngine = conflictEngine;
            _personnelService = personnelService;
        }

        public bool ShowAddDialog()
        {
            var editorVM = new PersonEditorViewModel(_uow, _conflictEngine, _personnelService);
            var dialog = new PersonEditorDialog(editorVM);
            return dialog.ShowDialog() == true;
        }

        public bool ShowEditDialog(Personnel person)
        {
            var editorVM = new PersonEditorViewModel(_uow, _conflictEngine, _personnelService, person);
            var dialog = new PersonEditorDialog(editorVM);
            return dialog.ShowDialog() == true;
        }
    }
}
