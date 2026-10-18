using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Picturebot.Messages;

namespace Picturebot.ViewModels;

/// <summary>
///     One running import, as shown in the status bar and the Processing Queue dialog.
/// </summary>
public partial class ImportActivityItem(Guid id) : ObservableObject {
    public Guid Id { get; } = id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Detail))]
    private string _albumName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Detail), nameof(IsIndeterminate), nameof(Percentage))]
    private ImportPhase _phase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail), nameof(Percentage))]
    private int _processed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Detail), nameof(IsIndeterminate), nameof(Percentage))]
    private int _total;

    public string Title => Phase == ImportPhase.Initializing
        ? $"Initializing album '{AlbumName}'…"
        : $"Importing '{AlbumName}'";

    public string Detail => Phase switch {
        ImportPhase.Initializing => "Looking for pictures",
        ImportPhase.Scanning => $"Reading file {Processed} of {Total}",
        ImportPhase.Copying => $"Copying picture {Processed} of {Total}",
        _ => string.Empty
    };

    public bool IsIndeterminate => Phase == ImportPhase.Initializing || Total == 0;

    public double Percentage => Total > 0 ? (double)Processed / Total * 100 : 0;

    public void Apply(ImportStatus status) {
        AlbumName = status.AlbumName;
        Phase = status.Phase;
        Processed = status.Processed;
        Total = status.Total;
    }
}
