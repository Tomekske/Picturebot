using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Database.Domain.Entities;

/// <summary>
///     Represents the global application settings and user preferences.
/// </summary>
[Table("settings")]
public class Settings {
    /// <summary>
    ///     The unique identifier for the settings record.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    ///     The preferred visual theme mode (e.g., "light", "dark", "system").
    /// </summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>
    ///     The absolute file system path to the root of the picture library.
    /// </summary>
    public string LibraryPath { get; set; } = string.Empty;

    /// <summary>
    ///     The Hamming distance threshold used for grouping similar pictures.
    /// </summary>
    public int GroupingThreshold { get; set; } = 5;

    /// <summary>
    ///     Maximum time gap in seconds to consider pictures as part of a burst without checking visual similarity.
    /// </summary>
    public int BurstTimeThresholdSeconds { get; set; } = 3;

    /// <summary>
    ///     Maximum time gap in seconds to consider pictures as part of a burst if they are visually similar.
    /// </summary>
    public int BurstFallbackTimeThresholdSeconds { get; set; } = 10;

    /// <summary>
    ///     Perceptual hash threshold for the fallback burst grouping.
    /// </summary>
    public int BurstHashSimilarityThreshold { get; set; } = 8;

    /// <summary>
    ///     Indicates whether the application should launch in a maximized window state.
    /// </summary>
    public bool LaunchMaximized { get; set; }
}
