using CommunityToolkit.Mvvm.ComponentModel;
using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance's live resource stats and user-defined name.
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject
{
    private readonly Action<int, string>? _onNameChanged;

    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private string _uptimeDisplay = string.Empty;
    [ObservableProperty] private string _slotName = string.Empty;

    public ClaudeInstanceViewModel(int instanceNumber, string initialName, Action<int, string>? onNameChanged = null)
    {
        _instanceNumber = instanceNumber;
        _slotName = initialName;
        _onNameChanged = onNameChanged;
    }

    partial void OnSlotNameChanged(string value)
    {
        _onNameChanged?.Invoke(InstanceNumber, value);
    }

    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        UptimeDisplay = snapshot.UptimeDisplay;
    }
}
