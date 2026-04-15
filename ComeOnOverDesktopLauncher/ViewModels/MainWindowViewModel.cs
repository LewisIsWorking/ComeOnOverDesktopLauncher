using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComeOnOverDesktopLauncher.Core.Models;
using ComeOnOverDesktopLauncher.Core.Services.Interfaces;

namespace ComeOnOverDesktopLauncher.ViewModels;

/// <summary>
/// Drives the main launcher window.
/// Handles launching Claude instances and the ComeOnOver app.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IClaudeInstanceLauncher _launcher;
    private readonly ISlotManager _slotManager;
    private readonly IComeOnOverAppService _cooService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private int _slotCount;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isClaudeInstalled;
    [ObservableProperty] private int _runningInstanceCount;

    public MainWindowViewModel(
        IClaudeInstanceLauncher launcher,
        ISlotManager slotManager,
        IComeOnOverAppService cooService,
        ISettingsService settingsService,
        IClaudePathResolver pathResolver)
    {
        _launcher = launcher;
        _slotManager = slotManager;
        _cooService = cooService;
        _settingsService = settingsService;

        var settings = _settingsService.Load();
        _slotCount = settings.DefaultSlotCount;
        _isClaudeInstalled = pathResolver.IsClaudeInstalled();
        _runningInstanceCount = _launcher.GetRunningInstanceCount();
    }

    [RelayCommand]
    private void LaunchInstances()
    {
        try
        {
            var slots = _slotManager.GetSlots(SlotCount);
            foreach (var slot in slots)
                _launcher.LaunchSlot(slot);

            RunningInstanceCount = _launcher.GetRunningInstanceCount();
            StatusMessage = $"Launched {SlotCount} instance(s). {RunningInstanceCount} running.";
            SaveSettings();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LaunchComeOnOver()
    {
        _cooService.Launch();
        StatusMessage = "ComeOnOver opened.";
    }

    [RelayCommand]
    private void RefreshInstanceCount()
    {
        RunningInstanceCount = _launcher.GetRunningInstanceCount();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Save(new AppSettings { DefaultSlotCount = SlotCount });
    }
}
