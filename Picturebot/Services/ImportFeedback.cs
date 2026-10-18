using System;
using CommunityToolkit.Mvvm.Messaging;
using Graph.Domain.DTOs;
using Picturebot.Messages;

namespace Picturebot.Services;

/// <summary>
///     Broadcasts the status of one import as <see cref="ImportStatusMessage" />s, so the status bar shows it from the
///     moment the dialog closes instead of only once the processing worker picks the pictures up.
/// </summary>
public sealed class ImportFeedback : IProgress<ImportProgress>, IProgress<ImportBatchProgress> {
    private readonly Guid _id = Guid.NewGuid();
    private string _albumName;

    private ImportFeedback(string albumName) {
        _albumName = albumName;
    }

    /// <summary>
    ///     Announces the import right away as "Initializing". Call it on the UI thread, before the import starts.
    /// </summary>
    public static ImportFeedback Start(string albumName) {
        var feedback = new ImportFeedback(albumName);
        feedback.Send(ImportPhase.Initializing, 0, 0);
        return feedback;
    }

    public void Report(ImportProgress value) {
        Send(value.Stage == ImportStage.Scanning ? ImportPhase.Scanning : ImportPhase.Copying, value.Processed, value.Total);
    }

    public void Report(ImportBatchProgress value) {
        _albumName = value.TotalAlbums > 1
            ? $"{value.CurrentAlbumName} ({value.ProcessedAlbums}/{value.TotalAlbums})"
            : value.CurrentAlbumName;

        if (value.CurrentAlbumProgress is { } albumProgress) {
            Report(albumProgress);
        } else {
            Send(ImportPhase.Initializing, 0, 0);
        }
    }

    public void Completed() {
        Send(ImportPhase.Completed, 0, 0);
    }

    public void Failed() {
        Send(ImportPhase.Failed, 0, 0);
    }

    private void Send(ImportPhase phase, int processed, int total) {
        WeakReferenceMessenger.Default.Send(new ImportStatusMessage(new ImportStatus(_id, _albumName, phase, processed, total)));
    }
}
