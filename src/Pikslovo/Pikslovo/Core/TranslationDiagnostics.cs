namespace Pikslovo.Core;

public sealed class TranslationDiagnostics
{
    private readonly object _lock = new();
    private TranslationDiagnosticsSnapshot _snapshot = new(null, null, null, null, null, null, null, null, null);

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
        long captureAndImageEncodingMilliseconds,
        long ocrImageEncodingMilliseconds,
        long cloudVisionOcrMilliseconds,
        long cloudTranslationMilliseconds,
        long totalMilliseconds)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with
            {
                CaptureAndImageEncodingMilliseconds = captureAndImageEncodingMilliseconds,
                OcrImageEncodingMilliseconds = ocrImageEncodingMilliseconds,
                CloudVisionOcrMilliseconds = cloudVisionOcrMilliseconds,
                CloudTranslationMilliseconds = cloudTranslationMilliseconds,
                TranslationTotalMilliseconds = totalMilliseconds,
            };
        }
    }

    public void RecordCaptureAttempt(
        CaptureAttemptStatus status,
        int attempts,
        long elapsedMilliseconds)
    {
        lock (_lock)
        {
            _snapshot = _snapshot with
            {
                LastCaptureAttemptStatus = status,
                LastCaptureAttemptCount = attempts,
                LastCaptureAttemptElapsedMilliseconds = elapsedMilliseconds,
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
    long? CaptureAndImageEncodingMilliseconds,
    long? OcrImageEncodingMilliseconds,
    long? CloudVisionOcrMilliseconds,
    long? CloudTranslationMilliseconds,
    long? TranslationTotalMilliseconds,
    long? ApiKeyValidationMilliseconds,
    CaptureAttemptStatus? LastCaptureAttemptStatus,
    int? LastCaptureAttemptCount,
    long? LastCaptureAttemptElapsedMilliseconds);

public enum CaptureAttemptStatus
{
    Success,
    NoFreshFrame,
    Failed
}
