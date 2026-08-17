using System;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Shell-level state shared between MainViewModel and feature ViewModels
    /// that need deployment header data (e.g. ReportsViewModel, DynamologioViewModel)
    /// or need to signal a full data reload (e.g. SettingsViewModel after restore).
    /// </summary>
    public interface IShellStateService
    {
        string UnitName { get; }
        string OfficeName { get; }

        void UpdateDeploymentHeader(string unitName, string officeName);
        event Action DeploymentHeaderChanged;

        void RequestFullRefresh();
        event Action FullRefreshRequested;
    }

    public class ShellStateService : IShellStateService
    {
        public string UnitName { get; private set; } = "ΜΟΝΑΔΑ";
        public string OfficeName { get; private set; } = "1ο ΓΡΑΦΕΙΟ";

        public event Action DeploymentHeaderChanged;
        public event Action FullRefreshRequested;

        public void UpdateDeploymentHeader(string unitName, string officeName)
        {
            UnitName = unitName ?? UnitName;
            OfficeName = officeName ?? OfficeName;
            DeploymentHeaderChanged?.Invoke();
        }

        public void RequestFullRefresh()
        {
            FullRefreshRequested?.Invoke();
        }
    }
}
