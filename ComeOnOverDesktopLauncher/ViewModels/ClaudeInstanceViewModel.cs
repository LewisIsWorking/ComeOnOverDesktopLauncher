using CommunityToolkit.Mvvm.ComponentModel;
using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance's live resource stats and user-defined name.
/// IsSeeded reflects whether login credentials have been copied to this slot.
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject
{
    private readonly Action<int, string>? _onNameChanged;

    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private double _ramMb;
    [ObservableProperty] private string _uptimeDisplay = string.Empty;
    [ObservableProperty] private string _slotName = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginStatusText))]
    [NotifyPropertyChangedFor(nameof(LoginStatusBackground))]
    [NotifyPropertyChangedFor(nameof(LoginStatusForeground))]
    private bool _isSeeded;

    public string LoginStatusText => IsSeeded ? "Logged in" : "Not logged in";
    public string LoginStatusBackground => IsSeeded ? "#2E7D32" : "#5D2F2F";
    public string LoginStatusForeground => IsSeeded ? "#81C784" : "#EF9A9A";
    public string LoginStatusTooltip => IsSeeded ? "Logged in" : "Not yet logged in - will log in on first launch";

    public ClaudeInstanceViewModel(
        int instanceNumber,
        string initialName,
        bool isSeeded,
        Action<int, string>? onNameChanged = null)
    {
        _instanceNumber = instanceNumber;
        _slotName = initialName;
        _isSeeded = isSeeded;
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




