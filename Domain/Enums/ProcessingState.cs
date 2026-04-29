namespace Domain.Enums;

/// <summary>
/// Represents the background processing state of a picture.
/// </summary>
public enum ProcessingState {
    /// <summary>
    /// Waiting for a worker to start processing.
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being processed by a worker.
    /// </summary>
    Processing,

    /// <summary>
    /// Successfully processed and analyzed.
    /// </summary>
    Completed,

    /// <summary>
    /// Processing failed after multiple attempts.
    /// </summary>
    Failed,

    /// <summary>
    /// Processing skipped (e.g., unsupported file format).
    /// </summary>
    Skipped
}
