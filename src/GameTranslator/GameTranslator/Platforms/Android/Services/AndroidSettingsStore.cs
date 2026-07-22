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
    int HotkeyCode,
    bool HoldToPreview,
    bool GlobalHotkeyEnabled);

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
    private const string HoldToPreviewName = "hold_to_preview";
    private const string GlobalHotkeyEnabledName = "global_hotkey_enabled";
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
            preferences.GetInt(HotkeyCodeName, 0),
            preferences.GetBoolean(HoldToPreviewName, false),
            preferences.GetBoolean(GlobalHotkeyEnabledName, false));
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
        editor.PutInt(HotkeyCodeName, settings.HotkeyCode);
        editor.PutBoolean(HoldToPreviewName, settings.HoldToPreview);
        editor.PutBoolean(GlobalHotkeyEnabledName, settings.GlobalHotkeyEnabled);
        editor.Apply();
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
