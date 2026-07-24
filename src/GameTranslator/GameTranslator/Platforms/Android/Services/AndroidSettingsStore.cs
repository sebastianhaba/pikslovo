using System.Text;
using Android.Content;
using Android.Security.Keystore;
using GameTranslator.Core;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace GameTranslator.Droid.Services;

internal sealed record AndroidAppSettings(
    TranslationSettings Translation,
    int[] HotkeyCodes,
    bool GlobalHotkeyEnabled,
    AppThemeMode ThemeMode,
    AppAccent Accent,
    FloatingButtonSettings FloatingButton);

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

internal static class AndroidSettingsStore
{
    private const string PreferencesName = "game_translator";
    private const string ApiKeyName = "google_api_key";
    private const string SourceLanguageName = "source_language";
    private const string TargetLanguageName = "target_language";
    private const string RecognitionConfidenceName = "recognition_confidence";
    private const string FontScaleName = "font_scale";
    private const string HideIdenticalTranslationsName = "hide_identical_translations";
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
    private const string KeyAlias = "game_translator_api_key";

    public static AndroidAppSettings Load(Context context)
    {
        var preferences = GetPreferences(context);
        return new AndroidAppSettings(
            new TranslationSettings(
                Decrypt(preferences.GetString(ApiKeyName, null)),
                preferences.GetString(SourceLanguageName, "ja") ?? "ja",
                preferences.GetString(TargetLanguageName, "pl") ?? "pl",
                preferences.GetFloat(RecognitionConfidenceName, TranslationSettings.DefaultRecognitionConfidence),
                preferences.GetFloat(FontScaleName, TranslationSettings.DefaultFontScale),
                preferences.GetBoolean(HideIdenticalTranslationsName, false)),
            ReadHotkeyCodes(preferences),
            preferences.GetBoolean(GlobalHotkeyEnabledName, false),
            ReadThemeMode(preferences.GetString(ThemeModeName, null)),
            ReadAccent(preferences.GetString(AccentName, null)),
            new FloatingButtonSettings(
                preferences.GetBoolean(FloatingButtonAlwaysVisibleName, true),
                Clamp(preferences.GetFloat(FloatingButtonScaleName, FloatingButtonSettings.DefaultScale), 0.5f, 2f),
                Clamp(preferences.GetFloat(FloatingButtonHorizontalPositionName, FloatingButtonSettings.DefaultHorizontalPosition), 0f, 1f),
                Clamp(preferences.GetFloat(FloatingButtonVerticalPositionName, FloatingButtonSettings.DefaultVerticalPosition), 0f, 1f)));
    }

    public static void Save(Context context, AndroidAppSettings settings)
    {
        using var editor = GetPreferences(context).Edit() ?? throw new InvalidOperationException("Nie można zapisać ustawień aplikacji.");
        editor.PutString(ApiKeyName, Encrypt(settings.Translation.ApiKey));
        editor.PutString(SourceLanguageName, settings.Translation.SourceLanguage);
        editor.PutString(TargetLanguageName, settings.Translation.TargetLanguage);
        editor.PutFloat(RecognitionConfidenceName, settings.Translation.RecognitionConfidence);
        editor.PutFloat(FontScaleName, settings.Translation.FontScale);
        editor.PutBoolean(HideIdenticalTranslationsName, settings.Translation.HideIdenticalTranslations);
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
        editor.Apply();
    }

    private static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);

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
