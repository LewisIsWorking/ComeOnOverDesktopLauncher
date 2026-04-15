using CommunityToolkit.Mvvm.ComponentModel;
using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance's live resource stats for display in the UI.
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject
{
    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private string _uptimeDisplay = string.Empty;

    public ClaudeInstanceViewModel(int instanceNumber)
    {
        _instanceNumber = instanceNumber;
    }

    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        UptimeDisplay = snapshot.UptimeDisplay;
    }
}
