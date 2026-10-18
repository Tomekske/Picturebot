using Domain.Enums;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Picturebot.Messages;

public class ProcessingProgressMessage(ProcessingProgress value) : ValueChangedMessage<ProcessingProgress>(value);

public class ProcessingCompletedMessage(int albumId) : ValueChangedMessage<int>(albumId);

public record ProcessingProgress(int AlbumId, int ProcessedCount, int TotalCount, string CurrentItemName);

/// <summary>
/// Sent when the worker starts on a picture. The value is the picture id.
/// </summary>
public class PictureProcessingStartedMessage(int pictureId) : ValueChangedMessage<int>(pictureId);

/// <summary>
/// Sent for every picture the worker finishes, unthrottled. <see cref="PictureProcessed.State"/> is the state the
/// picture ended in: Completed, Failed, or Pending when it will be retried.
/// </summary>
public class PictureProcessedMessage(PictureProcessed value) : ValueChangedMessage<PictureProcessed>(value);

public record PictureProcessed(int PictureId, int AlbumId, ProcessingState State);
