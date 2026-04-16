using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Wraps a single Claude instance''s live resource stats and user-defined name.
/// Exposes a Kill command so users can fully close an instance rather than letting
/// Claude minimize it to the system tray.
/// </summary>
public partial class ClaudeInstanceViewModel : ObservableObject
{
    private readonly Action<int, string>? _onNameChanged;
    private readonly Action<int>? _onKill;

    [ObservableProperty] private int _instanceNumber;
    [ObservableProperty] private int _processId;
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
    public string LoginStatusTooltip => IsSeeded
        ? "Logged in"
        : "Not yet logged in - will log in on first launch";

    public ClaudeInstanceViewModel(
        int instanceNumber,
        string initialName,
        bool isSeeded,
        Action<int, string>? onNameChanged = null,
        Action<int>? onKill = null)
    {
        _instanceNumber = instanceNumber;
        _slotName = initialName;
        _isSeeded = isSeeded;
        _onNameChanged = onNameChanged;
        _onKill = onKill;
    }

    partial void OnSlotNameChanged(string value)
    {
        _onNameChanged?.Invoke(InstanceNumber, value);
    }

    [RelayCommand]
    private void Kill()
    {
        _onKill?.Invoke(ProcessId);
    }

    public void UpdateFrom(InstanceResourceSnapshot snapshot)
    {
        ProcessId = snapshot.ProcessId;
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        UptimeDisplay = snapshot.UptimeDisplay;
    }
}
