namespace Pikslovo.Core;

public sealed class TranslationDiagnostics
{
    private readonly object _lock = new();
    private TranslationDiagnosticsSnapshot _snapshot = new(null, null, null, null, null);

    public TranslationDiagnosticsSnapshot Snapshot
    {
        get
        {
            lock (_lock)
            {
                return _snapshot;
            }
        }
    }

    public void RecordTranslation(
        long captureAndPngMilliseconds,
        long cloudVisionOcrMilliseconds,
        long cloudTranslationMilliseconds,
        long totalMilliseconds)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with
            {
                CaptureAndPngMilliseconds = captureAndPngMilliseconds,
                CloudVisionOcrMilliseconds = cloudVisionOcrMilliseconds,
                CloudTranslationMilliseconds = cloudTranslationMilliseconds,
                TranslationTotalMilliseconds = totalMilliseconds,
            };
        }
    }

    public void RecordApiKeyValidation(long milliseconds)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with { ApiKeyValidationMilliseconds = milliseconds };
        }
    }
}

public sealed record TranslationDiagnosticsSnapshot(
    long? CaptureAndPngMilliseconds,
    long? CloudVisionOcrMilliseconds,
    long? CloudTranslationMilliseconds,
    long? TranslationTotalMilliseconds,
    long? ApiKeyValidationMilliseconds);
