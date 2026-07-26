using Pikslovo.Core;

#if __ANDROID__
using Pikslovo.Droid.Services;
#endif

namespace Pikslovo;

public sealed class MainPageViewModel
{
    public string ApiKey { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "pl";
    public float RecognitionConfidence { get; set; } = TranslationSettings.DefaultRecognitionConfidence;
    public float GroupingPower { get; set; } = TranslationSettings.DefaultGroupingPower;
    public float FontScale { get; set; } = TranslationSettings.DefaultFontScale;
    public bool HideIdenticalTranslations { get; set; }
    public float OcrImageScale { get; set; } = TranslationSettings.DefaultOcrImageScale;
    public bool UseJpegForOcr { get; set; } = TranslationSettings.DefaultUseJpegForOcr;
    public int OcrJpegQuality { get; set; } = TranslationSettings.DefaultOcrJpegQuality;
    public int[] HotkeyCodes { get; set; } = [];
    public bool GlobalHotkeyEnabled { get; set; }
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;
    public AppAccent Accent { get; set; } = AppAccent.Lavender;
    public AppLanguageMode LanguageMode { get; set; } = AppLanguageMode.System;
    public bool FloatingButtonAlwaysVisible { get; set; } = true;
    public float FloatingButtonScale { get; set; } = 1f;
    public float FloatingButtonHorizontalPosition { get; set; } = 1f;
    public float FloatingButtonVerticalPosition { get; set; } = 0.1f;
    public string OnboardingSourceLanguage { get; set; } = "ja";
    public string OnboardingTargetLanguage { get; set; } = "pl";

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
        GlobalHotkeyEnabled = false;
        ThemeMode = AppThemeMode.System;
        Accent = AppAccent.Lavender;
        LanguageMode = AppLanguageMode.System;
        FloatingButtonAlwaysVisible = true;
        FloatingButtonScale = 1f;
        FloatingButtonHorizontalPosition = 1f;
        FloatingButtonVerticalPosition = 0.1f;
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
}
