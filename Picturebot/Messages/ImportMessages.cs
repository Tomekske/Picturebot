using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Picturebot.Messages;

public enum ImportPhase {
    Initializing,
    Scanning,
    Copying,
    Completed,
    Failed
}

/// <summary>
/// Status of one running import. <see cref="ImportStatus.Id"/> tells imports apart when several run at once.
/// </summary>
public record ImportStatus(Guid Id, string AlbumName, ImportPhase Phase, int Processed, int Total);

public class ImportStatusMessage(ImportStatus value) : ValueChangedMessage<ImportStatus>(value);
