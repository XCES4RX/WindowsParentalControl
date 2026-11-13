using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;

namespace ParentalControl.Admin.Services;

/// <summary>
/// Dynamic, reactive localization provider for WPF data binding.
/// Raising PropertyChanged with empty string causes all indexer bindings <c>{Binding [key]}</c>
/// across all active windows and user controls to re-evaluate instantly.
/// </summary>
public sealed class LocalizedStrings : ObservableObject
{
    public static LocalizedStrings Instance { get; } = new();

    private string _currentLanguage = "ar";
    private FlowDirection _flowDirection = FlowDirection.RightToLeft;

    private LocalizedStrings()
    {
        try
        {
            var settings = SettingsRepository.Get();
            _currentLanguage = SettingsRepository.NormalizeLanguage(settings.AppLanguage);
        }
        catch
        {
            _currentLanguage = "ar";
        }
        _flowDirection = _currentLanguage == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    public string this[string key] => LocalizationManager.T(key, _currentLanguage);

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            var norm = SettingsRepository.NormalizeLanguage(value);
            if (_currentLanguage != norm)
            {
                _currentLanguage = norm;
                FlowDirection = _currentLanguage == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                OnPropertyChanged(string.Empty);
            }
        }
    }

    public FlowDirection FlowDirection
    {
        get => _flowDirection;
        private set => SetProperty(ref _flowDirection, value);
    }

    public void ApplyLanguage(string lang)
    {
        CurrentLanguage = lang;
    }
}
