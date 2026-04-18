namespace Graph.Domain.DTOs;

public record ImportBatchProgress(
    int ProcessedAlbums,
    int TotalAlbums,
    string CurrentAlbumName,
    ImportProgress? CurrentAlbumProgress = null
) {
    public double OverallPercentage => TotalAlbums > 0 ? (double)ProcessedAlbums / TotalAlbums * 100 : 0;
}
