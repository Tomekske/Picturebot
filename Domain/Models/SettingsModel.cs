using Domain.Enums;

namespace Domain.Models;

public class SettingsModel {
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    public string? LibraryPath { get; set; }

    /// <summary>
    ///     Perceptual hash threshold for grouping similar pictures (Lower is stricter, 8-10 is typical).
    /// </summary>
    public int GroupingThreshold { get; set; } = 8;

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

    public bool LaunchMaximized { get; set; } = false;
}
