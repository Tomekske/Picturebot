namespace Graph.Domain.DTOs;

/// <summary>
/// The step of an album import that a progress report belongs to.
/// </summary>
public enum ImportStage {
    /// <summary>
    /// Reading capture dates, dimensions and orientation from the source files.
    /// </summary>
    Scanning,

    /// <summary>
    /// Copying the files into the library and adding them to the database.
    /// </summary>
    Copying
}

public record ImportProgress(int Processed, int Total, string CurrentFile, ImportStage Stage = ImportStage.Copying) {
    public double Percentage => Total > 0 ? (double)Processed / Total * 100 : 0;
}
