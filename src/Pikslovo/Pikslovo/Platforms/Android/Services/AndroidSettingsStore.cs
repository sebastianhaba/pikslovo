using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Pikslovo.Core;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace Pikslovo.Droid.Services;

internal sealed record AndroidAppSettings(
    TranslationSettings Translation,
    int[] HotkeyCodes,
    bool GlobalHotkeyEnabled,
    AppThemeMode ThemeMode,
    AppAccent Accent,
    FloatingButtonSettings FloatingButton,
    CaptureRegionSettings CaptureRegion);

internal sealed record FloatingButtonSettings(
    bool AlwaysVisible,
    float Scale,
    float HorizontalPosition,
    float VerticalPosition)
{
    public const float DefaultScale = 1f;
    public const float DefaultHorizontalPosition = 1f;
    public const float DefaultVerticalPosition = 0.1f;
}

internal sealed record CaptureRegionSettings(
    bool IsEnabled,
    float Left,
    float Top,
    float Right,
    float Bottom)
{
    public static CaptureRegionSettings FullScreen { get; } = new(false, 0f, 0f, 1f, 1f);

    public CaptureRegionSettings Normalize()
    {
        if (!IsEnabled)
        {
            return FullScreen;
        }

        var left = Math.Clamp(Math.Min(Left, Right), 0f, 0.95f);
        var top = Math.Clamp(Math.Min(Top, Bottom), 0f, 0.95f);
        var right = Math.Clamp(Math.Max(Left, Right), left + 0.05f, 1f);
        var bottom = Math.Clamp(Math.Max(Top, Bottom), top + 0.05f, 1f);
        return new CaptureRegionSettings(true, left, top, right, bottom);
    }

    public PixelRect ToPixelRect(int width, int height)
    {
        var region = Normalize();
        var left = (int)Math.Floor(width * region.Left);
        var top = (int)Math.Floor(height * region.Top);
        var right = (int)Math.Ceiling(width * region.Right);
        var bottom = (int)Math.Ceiling(height * region.Bottom);
        return new PixelRect(
            Math.Clamp(left, 0, Math.Max(0, width - 1)),
            Math.Clamp(top, 0, Math.Max(0, height - 1)),
            Math.Clamp(Math.Max(right, left + 1), 1, width),
            Math.Clamp(Math.Max(bottom, top + 1), 1, height));
    }
}

internal static class AndroidSettingsStore
{
    private const string PreferencesName = "game_translator";
    private const string ApiKeyName = "google_api_key";
    private const string SourceLanguageName = "source_language";
    private const string TargetLanguageName = "target_language";
    private const string RecognitionConfidenceName = "recognition_confidence";
    private const string GroupingPowerName = "grouping_power";
    private const string FontScaleName = "font_scale";
    private const string HideIdenticalTranslationsName = "hide_identical_translations";
    private const string OcrImageScaleName = "ocr_image_scale";
    private const string UseJpegForOcrName = "use_jpeg_for_ocr";
    private const string OcrJpegQualityName = "ocr_jpeg_quality";
    private const string HotkeyCodeName = "hotkey_code";
    private const string HotkeyCodesName = "hotkey_codes";
    private const string HoldToPreviewName = "hold_to_preview";
    private const string GlobalHotkeyEnabledName = "global_hotkey_enabled";
    private const string ThemeModeName = "theme_mode";
    private const string AccentName = "accent";
    private const string FloatingButtonAlwaysVisibleName = "floating_button_always_visible";
    private const string FloatingButtonScaleName = "floating_button_scale";
    private const string FloatingButtonHorizontalPositionName = "floating_button_horizontal_position";
    private const string FloatingButtonVerticalPositionName = "floating_button_vertical_position";
    private const string CaptureRegionEnabledName = "capture_region_enabled";
    private const string CaptureRegionLeftName = "capture_region_left";
    private const string CaptureRegionTopName = "capture_region_top";
    private const string CaptureRegionRightName = "capture_region_right";
    private const string CaptureRegionBottomName = "capture_region_bottom";
    private const string OnboardingCompletedName = "onboarding_completed";
    private const string KeyAlias = "game_translator_api_key";

    public static bool HasCompletedOnboarding(Context context) =>
        GetPreferences(context).GetBoolean(OnboardingCompletedName, false);

    public static void CompleteOnboarding(Context context)
    {
        using var editor = GetPreferences(context).Edit() ?? throw new InvalidOperationException("Nie można zapisać stanu konfiguracji aplikacji.");
        editor.PutBoolean(OnboardingCompletedName, true);
        editor.Apply();
    }

    public static AndroidAppSettings Load(Context context)
    {
        var preferences = GetPreferences(context);
        return new AndroidAppSettings(
            new TranslationSettings(
                Decrypt(preferences.GetString(ApiKeyName, null)),
                preferences.GetString(SourceLanguageName, "ja") ?? "ja",
                preferences.GetString(TargetLanguageName, "pl") ?? "pl",
                preferences.GetFloat(RecognitionConfidenceName, TranslationSettings.DefaultRecognitionConfidence),
                Clamp(preferences.GetFloat(GroupingPowerName, TranslationSettings.DefaultGroupingPower), TranslationSettings.DefaultGroupingPower, 1f),
                preferences.GetFloat(FontScaleName, TranslationSettings.DefaultFontScale),
                preferences.GetBoolean(HideIdenticalTranslationsName, false),
                NormalizeOcrImageScale(preferences.GetFloat(OcrImageScaleName, TranslationSettings.DefaultOcrImageScale)),
                preferences.GetBoolean(UseJpegForOcrName, TranslationSettings.DefaultUseJpegForOcr),
                NormalizeOcrJpegQuality(preferences.GetInt(OcrJpegQualityName, TranslationSettings.DefaultOcrJpegQuality))),
            ReadHotkeyCodes(preferences),
            preferences.GetBoolean(GlobalHotkeyEnabledName, false),
            ReadThemeMode(preferences.GetString(ThemeModeName, null)),
            ReadAccent(preferences.GetString(AccentName, null)),
            new FloatingButtonSettings(
                preferences.GetBoolean(FloatingButtonAlwaysVisibleName, true),
                Clamp(preferences.GetFloat(FloatingButtonScaleName, FloatingButtonSettings.DefaultScale), 0.5f, 2f),
                Clamp(preferences.GetFloat(FloatingButtonHorizontalPositionName, FloatingButtonSettings.DefaultHorizontalPosition), 0f, 1f),
                Clamp(preferences.GetFloat(FloatingButtonVerticalPositionName, FloatingButtonSettings.DefaultVerticalPosition), 0f, 1f)),
            new CaptureRegionSettings(
                preferences.GetBoolean(CaptureRegionEnabledName, false),
                preferences.GetFloat(CaptureRegionLeftName, 0f),
                preferences.GetFloat(CaptureRegionTopName, 0f),
                preferences.GetFloat(CaptureRegionRightName, 1f),
                preferences.GetFloat(CaptureRegionBottomName, 1f)).Normalize());
    }

    public static void Save(Context context, AndroidAppSettings settings)
    {
        using var editor = GetPreferences(context).Edit() ?? throw new InvalidOperationException("Nie można zapisać ustawień aplikacji.");
        editor.PutString(ApiKeyName, Encrypt(settings.Translation.ApiKey));
        editor.PutString(SourceLanguageName, settings.Translation.SourceLanguage);
        editor.PutString(TargetLanguageName, settings.Translation.TargetLanguage);
        editor.PutFloat(RecognitionConfidenceName, settings.Translation.RecognitionConfidence);
        editor.PutFloat(GroupingPowerName, Math.Clamp(settings.Translation.GroupingPower, TranslationSettings.DefaultGroupingPower, 1f));
        editor.PutFloat(FontScaleName, settings.Translation.FontScale);
        editor.PutBoolean(HideIdenticalTranslationsName, settings.Translation.HideIdenticalTranslations);
        editor.PutFloat(OcrImageScaleName, NormalizeOcrImageScale(settings.Translation.OcrImageScale));
        editor.PutBoolean(UseJpegForOcrName, settings.Translation.UseJpegForOcr);
        editor.PutInt(OcrJpegQualityName, NormalizeOcrJpegQuality(settings.Translation.OcrJpegQuality));
        editor.PutString(HotkeyCodesName, string.Join(',', settings.HotkeyCodes));
        editor.Remove(HotkeyCodeName);
        editor.Remove(HoldToPreviewName);
        editor.PutBoolean(GlobalHotkeyEnabledName, settings.GlobalHotkeyEnabled);
        editor.PutString(ThemeModeName, settings.ThemeMode.ToString());
        editor.PutString(AccentName, settings.Accent.ToString());
        editor.PutBoolean(FloatingButtonAlwaysVisibleName, settings.FloatingButton.AlwaysVisible);
        editor.PutFloat(FloatingButtonScaleName, Clamp(settings.FloatingButton.Scale, 0.5f, 2f));
        editor.PutFloat(FloatingButtonHorizontalPositionName, Clamp(settings.FloatingButton.HorizontalPosition, 0f, 1f));
        editor.PutFloat(FloatingButtonVerticalPositionName, Clamp(settings.FloatingButton.VerticalPosition, 0f, 1f));
        var captureRegion = settings.CaptureRegion.Normalize();
        editor.PutBoolean(CaptureRegionEnabledName, captureRegion.IsEnabled);
        editor.PutFloat(CaptureRegionLeftName, captureRegion.Left);
        editor.PutFloat(CaptureRegionTopName, captureRegion.Top);
        editor.PutFloat(CaptureRegionRightName, captureRegion.Right);
        editor.PutFloat(CaptureRegionBottomName, captureRegion.Bottom);
        editor.Apply();
    }

    private static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);

    private static float NormalizeOcrImageScale(float value) =>
        value switch
        {
            <= 0.375f => 0.25f,
            <= 0.625f => 0.5f,
            <= 0.875f => 0.75f,
            _ => 1f
        };

    private static int NormalizeOcrJpegQuality(int value) =>
        Math.Clamp(value, TranslationSettings.MinimumOcrJpegQuality, TranslationSettings.MaximumOcrJpegQuality);

    private static AppThemeMode ReadThemeMode(string? value) =>
        Enum.TryParse<AppThemeMode>(value, ignoreCase: true, out var mode) ? mode : AppThemeMode.System;

    private static AppAccent ReadAccent(string? value) =>
        Enum.TryParse<AppAccent>(value, ignoreCase: true, out var accent) ? accent : AppAccent.Lavender;

    private static int[] ReadHotkeyCodes(ISharedPreferences preferences)
    {
        var savedCodes = preferences.GetString(HotkeyCodesName, null);
        if (!string.IsNullOrWhiteSpace(savedCodes))
        {
            return savedCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var code) ? code : 0)
                .Where(code => code > 0)
                .Distinct()
                .ToArray();
        }

        var legacyCode = preferences.GetInt(HotkeyCodeName, 0);
        return legacyCode > 0 ? [legacyCode] : [];
    }

    private static ISharedPreferences GetPreferences(Context context) =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;

    private static string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var key = GetOrCreateKey();
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.EncryptMode, key);
        var iv = cipher.GetIV() ?? throw new InvalidOperationException("Android Keystore nie zwrócił wektora inicjalizacyjnego.");
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(plaintext)) ?? throw new InvalidOperationException("Android Keystore nie zaszyfrował klucza API.");
        return $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(encrypted)}";
    }

    private static string Decrypt(string? ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
        {
            return string.Empty;
        }

        var parts = ciphertext.Split(':', 2);
        if (parts.Length != 2)
        {
            return string.Empty;
        }

        try
        {
            var key = GetOrCreateKey();
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(
                CipherMode.DecryptMode,
                key,
                new GCMParameterSpec(128, Convert.FromBase64String(parts[0])));
            var plaintext = cipher.DoFinal(Convert.FromBase64String(parts[1]));
            return plaintext is null ? string.Empty : Encoding.UTF8.GetString(plaintext);
        }
        catch (GeneralSecurityException)
        {
            return string.Empty;
        }
    }

    private static ISecretKey GetOrCreateKey()
    {
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore")!;
        keyStore.Load(null);
        if (keyStore.ContainsAlias(KeyAlias))
        {
            var entry = keyStore.GetEntry(KeyAlias, null) as KeyStore.SecretKeyEntry;
            return entry?.SecretKey ?? throw new InvalidOperationException("Nie można odczytać klucza Android Keystore.");
        }

        using var keyGenerator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        var specification = new KeyGenParameterSpec.Builder(
                KeyAlias,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .Build();
        keyGenerator.Init(specification);
        return keyGenerator.GenerateKey()!;
    }
}
