using GameTranslator.Core;

namespace GameTranslator.Services;

public static class AppServices
{
    private static readonly HttpClient HttpClient = new();

    public static TranslationOrchestrator TranslationOrchestrator { get; } = new(
        new GoogleVisionOcrProvider(HttpClient),
        new GoogleTranslationProvider(HttpClient));
}
