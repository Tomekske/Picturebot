namespace Domain.Messages;

/// <summary>
/// Message sent when a batch of curation items has been fully processed.
/// The value is the number of items that were processed in the batch.
/// </summary>
public class CurationCompletedMessage(int count) {
    public int Count { get; } = count;
}
