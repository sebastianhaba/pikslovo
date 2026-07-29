using CommunityToolkit.Mvvm.Input;
using Pikslovo.Core;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

#if __ANDROID__
using Pikslovo.Droid.Services;
#endif

namespace Pikslovo;

public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private static readonly ICommand NoOpCommand = new RelayCommand(() => { });
    private static readonly ICommand NoOpStringCommand = new RelayCommand<string>(_ => { });
    private string _apiKey = string.Empty;
    private string _sourceLanguage = "ja";
    private string _targetLanguage = "pl";
    private float _recognitionConfidence = TranslationSettings.DefaultRecognitionConfidence;
    private float _groupingPower = TranslationSettings.DefaultGroupingPower;
    private float _fontScale = TranslationSettings.DefaultFontScale;
    private bool _hideIdenticalTranslations = TranslationSettings.DefaultHideIdenticalTranslations;
    private float _ocrImageScale = TranslationSettings.DefaultOcrImageScale;
    private bool _useJpegForOcr = TranslationSettings.DefaultUseJpegForOcr;
    private int _ocrJpegQuality = TranslationSettings.DefaultOcrJpegQuality;
    private int[] _hotkeyCodes = [];
    private string _hotkeyCodesSummary = AppStrings.Get(AppStrings.Keys.NotSet);
    private bool _globalHotkeyEnabled;
    private bool _hasAccessibilityPermission;
    private AppThemeMode _themeMode = AppThemeMode.System;
    private AppAccent _accent = AppAccent.Lavender;
    private AppLanguageMode _languageMode = AppLanguageMode.System;
    private bool _floatingButtonAlwaysVisible = true;
    private float _floatingButtonScale = 1f;
    private float _floatingButtonHorizontalPosition = 0.97f;
    private float _floatingButtonVerticalPosition = 0.2f;
    private string _onboardingSourceLanguage = "ja";
    private string _onboardingTargetLanguage = "pl";
    private string _captureAndImageEncodingDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _ocrImageEncodingDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _ocrPayloadSize = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _cloudVisionOcrDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _cloudTranslationDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _overlayRenderDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _translationTotalDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _apiKeyValidationDuration = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _lastCaptureAttemptStatus = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _lastCaptureAttemptCount = AppStrings.Get(AppStrings.Keys.NoMeasurement);
    private string _lastCaptureAttemptElapsed = AppStrings.Get(AppStrings.Keys.NoMeasurement);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand BackCommand { get; set; } = NoOpCommand;

    public ICommand OpenSectionCommand { get; set; } = NoOpStringCommand;

    public ICommand ExportSettingsCommand { get; set; } = NoOpCommand;

    public ICommand ImportSettingsCommand { get; set; } = NoOpCommand;

    public ICommand RestoreDefaultSettingsCommand { get; set; } = NoOpCommand;

    public ICommand ExportDiagnosticsCommand { get; set; } = NoOpCommand;

    public ICommand OpenGitHubPageCommand { get; set; } = NoOpCommand;

    public ICommand OpenWikiPageCommand { get; set; } = NoOpCommand;

    public ICommand SharePikslovoCommand { get; set; } = NoOpCommand;

    public ICommand OpenSupportPageCommand { get; set; } = NoOpCommand;

    public ICommand EditSourceLanguageCommand { get; set; } = NoOpCommand;

    public ICommand EditTargetLanguageCommand { get; set; } = NoOpCommand;

    public ICommand ToggleApiKeyVisibilityCommand { get; set; } = NoOpCommand;

    public ICommand OpenGoogleCloudApiKeyGuideCommand { get; set; } = NoOpCommand;

    public ICommand OpenAccessibilitySettingsCommand { get; set; } = NoOpCommand;

    public ICommand EditHotkeyCodeCommand { get; set; } = NoOpCommand;

    public ICommand OpenHotkeyBlockedHelpCommand { get; set; } = NoOpCommand;

    public ICommand RequestOverlayPermissionCommand { get; set; } = NoOpCommand;

    public ICommand RequestNotificationPermissionCommand { get; set; } = NoOpCommand;

    public ICommand TestApiKeyCommand { get; set; } = NoOpCommand;

    public ICommand SelectThemeModeCommand { get; set; } = NoOpStringCommand;

    public ICommand SelectAccentCommand { get; set; } = NoOpStringCommand;

    public ICommand SelectApplicationLanguageCommand { get; set; } = NoOpStringCommand;

    public ICommand EditOnboardingSourceLanguageCommand { get; set; } = NoOpCommand;

    public ICommand EditOnboardingTargetLanguageCommand { get; set; } = NoOpCommand;

    public ICommand ContinueOnboardingLanguageCommand { get; set; } = NoOpCommand;

    public ICommand RequestOnboardingNotificationPermissionCommand { get; set; } = NoOpCommand;

    public ICommand RequestOnboardingOverlayPermissionCommand { get; set; } = NoOpCommand;

    public ICommand TestOnboardingApiKeyCommand { get; set; } = NoOpCommand;

    public ICommand FinishOnboardingCommand { get; set; } = NoOpCommand;

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
        set
        {
            if (!SetProperty(ref _hotkeyCodes, value))
            {
                return;
            }

            OnPropertyChanged(nameof(GlobalHotkeySummary));
        }
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
            OnPropertyChanged(nameof(GlobalHotkeySummary));
        }
    }

    public bool HasAccessibilityPermission
    {
        get => _hasAccessibilityPermission;
        set
        {
            if (!SetProperty(ref _hasAccessibilityPermission, value))
            {
                return;
            }

            OnPropertyChanged(nameof(GlobalHotkeySummary));
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

    public string SourceLanguageSummary => AppStrings.GetLanguageName(SourceLanguage);

    public string TargetLanguageSummary => AppStrings.GetLanguageName(TargetLanguage);

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
            ? AppStrings.Get(AppStrings.Keys.ItIsVisibleDuringAnActiveSessionRegardlessOfTheGlobalHotkey)
            : GlobalHotkeyEnabled
                ? AppStrings.Get(AppStrings.Keys.ItIsHiddenDuringAnActiveSessionWhenTheGlobalHotkeyIsEnabled)
                : AppStrings.Get(AppStrings.Keys.ItIsVisibleDuringAnActiveSessionBecauseTheGlobalHotkeyIsDisabled);

    public string GlobalHotkeySummary =>
        !HasAccessibilityPermission
            ? AppStrings.Get(AppStrings.Keys.EnableTheSystemServiceForTheGlobalHotkey)
            : GlobalHotkeyEnabled && HotkeyCodes.Length > 0
                ? HotkeyCodesSummary
                : AppStrings.Get(AppStrings.Keys.TapToSetShortcut);

    public string ThemeModeSummary => $"{AppStrings.GetThemeModeLabel(ThemeMode)} · {AppStrings.GetAccentLabel(Accent)}";

    public string OnboardingSourceLanguageLabel => AppStrings.GetLanguageName(OnboardingSourceLanguage);

    public string OnboardingTargetLanguageLabel => AppStrings.GetLanguageName(OnboardingTargetLanguage);

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

    public string OcrPayloadSize
    {
        get => _ocrPayloadSize;
        private set => SetProperty(ref _ocrPayloadSize, value);
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

    public string OverlayRenderDuration
    {
        get => _overlayRenderDuration;
        private set => SetProperty(ref _overlayRenderDuration, value);
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

    public string LastCaptureAttemptStatus
    {
        get => _lastCaptureAttemptStatus;
        private set => SetProperty(ref _lastCaptureAttemptStatus, value);
    }

    public string LastCaptureAttemptCount
    {
        get => _lastCaptureAttemptCount;
        private set => SetProperty(ref _lastCaptureAttemptCount, value);
    }

    public string LastCaptureAttemptElapsed
    {
        get => _lastCaptureAttemptElapsed;
        private set => SetProperty(ref _lastCaptureAttemptElapsed, value);
    }

    public void LoadDefaults()
    {
        ApiKey = string.Empty;
        SourceLanguage = "ja";
        TargetLanguage = "pl";
        RecognitionConfidence = TranslationSettings.DefaultRecognitionConfidence;
        GroupingPower = TranslationSettings.DefaultGroupingPower;
        FontScale = TranslationSettings.DefaultFontScale;
        HideIdenticalTranslations = TranslationSettings.DefaultHideIdenticalTranslations;
        OcrImageScale = TranslationSettings.DefaultOcrImageScale;
        UseJpegForOcr = TranslationSettings.DefaultUseJpegForOcr;
        OcrJpegQuality = TranslationSettings.DefaultOcrJpegQuality;
        HotkeyCodes = [];
        HotkeyCodesSummary = AppStrings.Get(AppStrings.Keys.NotSet);
        GlobalHotkeyEnabled = false;
        HasAccessibilityPermission = false;
        ThemeMode = AppThemeMode.System;
        Accent = AppAccent.Lavender;
        LanguageMode = AppLanguageMode.System;
        FloatingButtonAlwaysVisible = true;
        FloatingButtonScale = 1f;
        FloatingButtonHorizontalPosition = 0.97f;
        FloatingButtonVerticalPosition = 0.2f;
        OnboardingSourceLanguage = "ja";
        OnboardingTargetLanguage = "pl";
        UpdateDiagnostics(new TranslationDiagnosticsSnapshot(null, null, null, null, null, null, null, null, null, null, null));
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

    public void SetHotkeyCodesSummary(string summary)
    {
        HotkeyCodesSummary = summary;
        OnPropertyChanged(nameof(GlobalHotkeySummary));
    }

    public void UpdateDiagnostics(TranslationDiagnosticsSnapshot diagnostics)
    {
        CaptureAndImageEncodingDuration = FormatDuration(diagnostics.CaptureAndImageEncodingMilliseconds);
        OcrImageEncodingDuration = FormatDuration(diagnostics.OcrImageEncodingMilliseconds);
        OcrPayloadSize = FormatImageSize(diagnostics.OcrImageBytes);
        CloudVisionOcrDuration = FormatDuration(diagnostics.CloudVisionOcrMilliseconds);
        CloudTranslationDuration = FormatDuration(diagnostics.CloudTranslationMilliseconds);
        OverlayRenderDuration = FormatDuration(diagnostics.OverlayRenderMilliseconds);
        TranslationTotalDuration = FormatDuration(diagnostics.TranslationTotalMilliseconds);
        ApiKeyValidationDuration = FormatDuration(diagnostics.ApiKeyValidationMilliseconds);
        LastCaptureAttemptStatus = FormatCaptureAttemptStatus(diagnostics.LastCaptureAttemptStatus);
        LastCaptureAttemptCount = FormatCount(diagnostics.LastCaptureAttemptCount);
        LastCaptureAttemptElapsed = FormatDuration(diagnostics.LastCaptureAttemptElapsedMilliseconds);
    }

    public static bool IsAutoPersistedProperty(string? propertyName) => propertyName is
        nameof(RecognitionConfidence) or
        nameof(GroupingPower) or
        nameof(FontScale) or
        nameof(HideIdenticalTranslations) or
        nameof(OcrImageScale) or
        nameof(UseJpegForOcr) or
        nameof(OcrJpegQuality) or
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

    private static string FormatScale(float value) => $"{value.ToString("0.0", CultureInfo.CurrentCulture)}x";

    private static string FormatDuration(long? milliseconds) => milliseconds is { } value
        ? $"{value} ms"
        : AppStrings.Get(AppStrings.Keys.NoMeasurement);

    private static string FormatCount(int? value) => value is { } count
        ? count.ToString(CultureInfo.CurrentCulture)
        : AppStrings.Get(AppStrings.Keys.NoMeasurement);

    private static string FormatImageSize(long? bytes) => bytes is { } size
        ? $"{size / 1024d:0.0} KiB"
        : AppStrings.Get(AppStrings.Keys.NoMeasurement);

    private static string FormatCaptureAttemptStatus(CaptureAttemptStatus? status) => status switch
    {
        CaptureAttemptStatus.Success => AppStrings.Get(AppStrings.Keys.DiagnosticsCaptureStatusSuccess),
        CaptureAttemptStatus.NoFreshFrame => AppStrings.Get(AppStrings.Keys.DiagnosticsCaptureStatusNoFreshFrame),
        CaptureAttemptStatus.Failed => AppStrings.Get(AppStrings.Keys.DiagnosticsCaptureStatusFailed),
        _ => AppStrings.Get(AppStrings.Keys.NoMeasurement),
    };

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
