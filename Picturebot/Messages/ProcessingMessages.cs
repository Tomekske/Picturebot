using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Picturebot.Messages;

public class ProcessingProgressMessage(ProcessingProgress value) : ValueChangedMessage<ProcessingProgress>(value);

public class ProcessingCompletedMessage(int albumId) : ValueChangedMessage<int>(albumId);

public record ProcessingProgress(int AlbumId, int ProcessedCount, int TotalCount, string CurrentItemName);
