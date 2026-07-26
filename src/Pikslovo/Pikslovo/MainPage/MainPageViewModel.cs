using Pikslovo.Core;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

#if __ANDROID__
using Pikslovo.Droid.Services;
#endif

namespace Pikslovo;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private string _apiKey = string.Empty;
    private string _sourceLanguage = "ja";
    private string _targetLanguage = "pl";
    private float _recognitionConfidence = TranslationSettings.DefaultRecognitionConfidence;
    private float _groupingPower = TranslationSettings.DefaultGroupingPower;
    private float _fontScale = TranslationSettings.DefaultFontScale;
    private bool _hideIdenticalTranslations;
    private float _ocrImageScale = TranslationSettings.DefaultOcrImageScale;
    private bool _useJpegForOcr = TranslationSettings.DefaultUseJpegForOcr;
    private int _ocrJpegQuality = TranslationSettings.DefaultOcrJpegQuality;
    private int[] _hotkeyCodes = [];
    private string _hotkeyCodesSummary = AppStrings.Get("Nie ustawiono");
    private bool _globalHotkeyEnabled;
    private AppThemeMode _themeMode = AppThemeMode.System;
    private AppAccent _accent = AppAccent.Lavender;
    private AppLanguageMode _languageMode = AppLanguageMode.System;
    private bool _floatingButtonAlwaysVisible = true;
    private float _floatingButtonScale = 1f;
    private float _floatingButtonHorizontalPosition = 1f;
    private float _floatingButtonVerticalPosition = 0.1f;
    private string _onboardingSourceLanguage = "ja";
    private string _onboardingTargetLanguage = "pl";
    private string _captureAndImageEncodingDuration = AppStrings.Get("Brak pomiaru");
    private string _ocrImageEncodingDuration = AppStrings.Get("Brak pomiaru");
    private string _cloudVisionOcrDuration = AppStrings.Get("Brak pomiaru");
    private string _cloudTranslationDuration = AppStrings.Get("Brak pomiaru");
    private string _translationTotalDuration = AppStrings.Get("Brak pomiaru");
    private string _apiKeyValidationDuration = AppStrings.Get("Brak pomiaru");

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string SourceLanguage
    {
        get => _sourceLanguage;
        set
        {
            if (!SetProperty(ref _sourceLanguage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SourceLanguageSummary));
        }
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (!SetProperty(ref _targetLanguage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TargetLanguageSummary));
        }
    }

    public float RecognitionConfidence
    {
        get => _recognitionConfidence;
        set
        {
            if (!SetProperty(ref _recognitionConfidence, value))
            {
                return;
            }

            OnPropertyChanged(nameof(RecognitionConfidenceDisplay));
        }
    }

    public float GroupingPower
    {
        get => _groupingPower;
        set
        {
            if (!SetProperty(ref _groupingPower, value))
            {
                return;
            }

            OnPropertyChanged(nameof(GroupingPowerDisplay));
        }
    }

    public float FontScale
    {
        get => _fontScale;
        set
        {
            if (!SetProperty(ref _fontScale, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FontScaleDisplay));
        }
    }

    public bool HideIdenticalTranslations
    {
        get => _hideIdenticalTranslations;
        set => SetProperty(ref _hideIdenticalTranslations, value);
    }

    public float OcrImageScale
    {
        get => _ocrImageScale;
        set
        {
            if (!SetProperty(ref _ocrImageScale, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OcrImageScaleDisplay));
        }
    }

    public bool UseJpegForOcr
    {
        get => _useJpegForOcr;
        set
        {
            if (!SetProperty(ref _useJpegForOcr, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsOcrJpegQualityEnabled));
            OnPropertyChanged(nameof(OcrJpegQualityOpacity));
        }
    }

    public int OcrJpegQuality
    {
        get => _ocrJpegQuality;
        set
        {
            if (!SetProperty(ref _ocrJpegQuality, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OcrJpegQualityDisplay));
        }
    }

    public int[] HotkeyCodes
    {
        get => _hotkeyCodes;
        set => SetProperty(ref _hotkeyCodes, value);
    }

    public string HotkeyCodesSummary
    {
        get => _hotkeyCodesSummary;
        private set => SetProperty(ref _hotkeyCodesSummary, value);
    }

    public bool GlobalHotkeyEnabled
    {
        get => _globalHotkeyEnabled;
        set
        {
            if (!SetProperty(ref _globalHotkeyEnabled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FloatingButtonVisibilityDescription));
        }
    }

    public AppThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (!SetProperty(ref _themeMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemeModeSummary));
        }
    }

    public AppAccent Accent
    {
        get => _accent;
        set
        {
            if (!SetProperty(ref _accent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemeModeSummary));
        }
    }

    public AppLanguageMode LanguageMode
    {
        get => _languageMode;
        set => SetProperty(ref _languageMode, value);
    }

    public bool FloatingButtonAlwaysVisible
    {
        get => _floatingButtonAlwaysVisible;
        set
        {
            if (!SetProperty(ref _floatingButtonAlwaysVisible, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FloatingButtonVisibilityDescription));
        }
    }

    public float FloatingButtonScale
    {
        get => _floatingButtonScale;
        set
        {
            if (!SetProperty(ref _floatingButtonScale, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FloatingButtonScaleDisplay));
        }
    }

    public float FloatingButtonHorizontalPosition
    {
        get => _floatingButtonHorizontalPosition;
        set
        {
            if (!SetProperty(ref _floatingButtonHorizontalPosition, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FloatingButtonHorizontalPositionDisplay));
        }
    }

    public float FloatingButtonVerticalPosition
    {
        get => _floatingButtonVerticalPosition;
        set
        {
            if (!SetProperty(ref _floatingButtonVerticalPosition, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FloatingButtonVerticalPositionDisplay));
        }
    }

    public string OnboardingSourceLanguage
    {
        get => _onboardingSourceLanguage;
        set
        {
            if (!SetProperty(ref _onboardingSourceLanguage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OnboardingSourceLanguageLabel));
        }
    }

    public string OnboardingTargetLanguage
    {
        get => _onboardingTargetLanguage;
        set
        {
            if (!SetProperty(ref _onboardingTargetLanguage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OnboardingTargetLanguageLabel));
        }
    }

    public string SourceLanguageSummary => GetLanguageName(SourceLanguage);

    public string TargetLanguageSummary => GetLanguageName(TargetLanguage);

    public string RecognitionConfidenceDisplay => RecognitionConfidence.ToString("0.0", CultureInfo.CurrentCulture);

    public string GroupingPowerDisplay => GroupingPower.ToString("0.00", CultureInfo.CurrentCulture);

    public string FontScaleDisplay => FormatScale(FontScale);

    public string OcrImageScaleDisplay => $"{OcrImageScale.ToString("0.##", CultureInfo.CurrentCulture)}x";

    public string OcrJpegQualityDisplay => $"{OcrJpegQuality:0}%";

    public bool IsOcrJpegQualityEnabled => UseJpegForOcr;

    public double OcrJpegQualityOpacity => UseJpegForOcr ? 1d : 0.45d;

    public string FloatingButtonScaleDisplay => FormatScale(FloatingButtonScale);

    public string FloatingButtonHorizontalPositionDisplay => FloatingButtonHorizontalPosition.ToString("0.00", CultureInfo.CurrentCulture);

    public string FloatingButtonVerticalPositionDisplay => FloatingButtonVerticalPosition.ToString("0.00", CultureInfo.CurrentCulture);

    public string FloatingButtonVisibilityDescription =>
        FloatingButtonAlwaysVisible
            ? "W aktywnej sesji jest widoczny niezależnie od globalnego hotkeya."
            : GlobalHotkeyEnabled
                ? "W aktywnej sesji jest ukryty, gdy globalny hotkey jest włączony."
                : "W aktywnej sesji jest widoczny, ponieważ globalny hotkey jest wyłączony.";

    public string ThemeModeSummary => $"{GetThemeModeLabel(ThemeMode)} · {GetAccentLabel(Accent)}";

    public string OnboardingSourceLanguageLabel => GetLanguageName(OnboardingSourceLanguage);

    public string OnboardingTargetLanguageLabel => GetLanguageName(OnboardingTargetLanguage);

    public string CaptureAndImageEncodingDuration
    {
        get => _captureAndImageEncodingDuration;
        private set => SetProperty(ref _captureAndImageEncodingDuration, value);
    }

    public string OcrImageEncodingDuration
    {
        get => _ocrImageEncodingDuration;
        private set => SetProperty(ref _ocrImageEncodingDuration, value);
    }

    public string CloudVisionOcrDuration
    {
        get => _cloudVisionOcrDuration;
        private set => SetProperty(ref _cloudVisionOcrDuration, value);
    }

    public string CloudTranslationDuration
    {
        get => _cloudTranslationDuration;
        private set => SetProperty(ref _cloudTranslationDuration, value);
    }

    public string TranslationTotalDuration
    {
        get => _translationTotalDuration;
        private set => SetProperty(ref _translationTotalDuration, value);
    }

    public string ApiKeyValidationDuration
    {
        get => _apiKeyValidationDuration;
        private set => SetProperty(ref _apiKeyValidationDuration, value);
    }

    public void LoadDefaults()
    {
        ApiKey = string.Empty;
        SourceLanguage = "ja";
        TargetLanguage = "pl";
        RecognitionConfidence = TranslationSettings.DefaultRecognitionConfidence;
        GroupingPower = TranslationSettings.DefaultGroupingPower;
        FontScale = TranslationSettings.DefaultFontScale;
        HideIdenticalTranslations = false;
        OcrImageScale = TranslationSettings.DefaultOcrImageScale;
        UseJpegForOcr = TranslationSettings.DefaultUseJpegForOcr;
        OcrJpegQuality = TranslationSettings.DefaultOcrJpegQuality;
        HotkeyCodes = [];
        HotkeyCodesSummary = AppStrings.Get("Nie ustawiono");
        GlobalHotkeyEnabled = false;
        ThemeMode = AppThemeMode.System;
        Accent = AppAccent.Lavender;
        LanguageMode = AppLanguageMode.System;
        FloatingButtonAlwaysVisible = true;
        FloatingButtonScale = 1f;
        FloatingButtonHorizontalPosition = 1f;
        FloatingButtonVerticalPosition = 0.1f;
        OnboardingSourceLanguage = "ja";
        OnboardingTargetLanguage = "pl";
        UpdateDiagnostics(new TranslationDiagnosticsSnapshot(null, null, null, null, null, null));
    }

    public TranslationSettings CreateTranslationSettings() =>
        new(
            ApiKey.Trim(),
            SourceLanguage,
            TargetLanguage,
            RecognitionConfidence,
            GroupingPower,
            FontScale,
            HideIdenticalTranslations,
            OcrImageScale,
            UseJpegForOcr,
            OcrJpegQuality);

    public void SetHotkeyCodesSummary(string summary) => HotkeyCodesSummary = summary;

    public void UpdateDiagnostics(TranslationDiagnosticsSnapshot diagnostics)
    {
        CaptureAndImageEncodingDuration = FormatDuration(diagnostics.CaptureAndImageEncodingMilliseconds);
        OcrImageEncodingDuration = FormatDuration(diagnostics.OcrImageEncodingMilliseconds);
        CloudVisionOcrDuration = FormatDuration(diagnostics.CloudVisionOcrMilliseconds);
        CloudTranslationDuration = FormatDuration(diagnostics.CloudTranslationMilliseconds);
        TranslationTotalDuration = FormatDuration(diagnostics.TranslationTotalMilliseconds);
        ApiKeyValidationDuration = FormatDuration(diagnostics.ApiKeyValidationMilliseconds);
    }

    public static bool IsAutoPersistedProperty(string? propertyName) => propertyName is
        nameof(RecognitionConfidence) or
        nameof(GroupingPower) or
        nameof(FontScale) or
        nameof(HideIdenticalTranslations) or
        nameof(OcrImageScale) or
        nameof(UseJpegForOcr) or
        nameof(OcrJpegQuality) or
        nameof(GlobalHotkeyEnabled) or
        nameof(FloatingButtonAlwaysVisible) or
        nameof(FloatingButtonScale) or
        nameof(FloatingButtonHorizontalPosition) or
        nameof(FloatingButtonVerticalPosition);

    public static bool RequiresFloatingButtonRefresh(string? propertyName) => propertyName is
        nameof(GlobalHotkeyEnabled) or
        nameof(FloatingButtonAlwaysVisible) or
        nameof(FloatingButtonScale) or
        nameof(FloatingButtonHorizontalPosition) or
        nameof(FloatingButtonVerticalPosition);

#if __ANDROID__
    internal void Apply(AndroidAppSettings settings)
    {
        ApiKey = settings.Translation.ApiKey;
        SourceLanguage = settings.Translation.SourceLanguage;
        TargetLanguage = settings.Translation.TargetLanguage;
        RecognitionConfidence = settings.Translation.RecognitionConfidence;
        GroupingPower = settings.Translation.GroupingPower;
        FontScale = settings.Translation.FontScale;
        HideIdenticalTranslations = settings.Translation.HideIdenticalTranslations;
        OcrImageScale = settings.Translation.OcrImageScale;
        UseJpegForOcr = settings.Translation.UseJpegForOcr;
        OcrJpegQuality = settings.Translation.OcrJpegQuality;
        HotkeyCodes = settings.HotkeyCodes;
        GlobalHotkeyEnabled = settings.GlobalHotkeyEnabled;
        ThemeMode = settings.ThemeMode;
        Accent = settings.Accent;
        LanguageMode = settings.LanguageMode;
        FloatingButtonAlwaysVisible = settings.FloatingButton.AlwaysVisible;
        FloatingButtonScale = settings.FloatingButton.Scale;
        FloatingButtonHorizontalPosition = settings.FloatingButton.HorizontalPosition;
        FloatingButtonVerticalPosition = settings.FloatingButton.VerticalPosition;
    }

    internal AndroidAppSettings ToAndroidSettings(CaptureRegionSettings captureRegion) =>
        new(
            CreateTranslationSettings(),
            HotkeyCodes,
            GlobalHotkeyEnabled,
            ThemeMode,
            Accent,
            LanguageMode,
            new FloatingButtonSettings(
                FloatingButtonAlwaysVisible,
                FloatingButtonScale,
                FloatingButtonHorizontalPosition,
                FloatingButtonVerticalPosition),
            captureRegion);
#endif

    private static string GetThemeModeLabel(AppThemeMode mode) => AppStrings.Get(mode switch
    {
        AppThemeMode.Dark => "Ciemny",
        AppThemeMode.Light => "Jasny",
        _ => "System"
    });

    private static string GetAccentLabel(AppAccent accent) => AppStrings.Get(accent switch
    {
        AppAccent.Coral => "Koralowy",
        AppAccent.Amber => "Bursztynowy",
        AppAccent.Lime => "Limonkowy",
        AppAccent.Mint => "Miętowy",
        AppAccent.Teal => "Morski",
        AppAccent.Aqua => "Aqua",
        AppAccent.Sky => "Błękitny",
        AppAccent.Steel => "Stalowy",
        AppAccent.Orchid => "Orchidea",
        AppAccent.Rose => "Różowy",
        _ => "Lawendowy"
    });

    private static string GetLanguageName(string language) => AppStrings.Get(language switch
    {
        "ja" => "Japoński",
        "en" => "Angielski",
        "ko" => "Koreański",
        "zh" => "Chiński (uproszczony)",
        "de" => "Niemiecki",
        "es" => "Hiszpański",
        _ => "Polski"
    });

    private static string FormatScale(float value) => $"{value.ToString("0.0", CultureInfo.CurrentCulture)}x";

    private static string FormatDuration(long? milliseconds) => milliseconds is { } value
        ? $"{value} ms"
        : AppStrings.Get("Brak pomiaru");

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
