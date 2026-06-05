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

    public bool LaunchMaximized { get; set; } = false;

    public string RedLabelName { get; set; } = "Red";
    public string OrangeLabelName { get; set; } = "Orange";
    public string YellowLabelName { get; set; } = "Yellow";
    public string GreenLabelName { get; set; } = "Green";
    public string BlueLabelName { get; set; } = "Blue";
    public string PinkLabelName { get; set; } = "Pink";
    public string PurpleLabelName { get; set; } = "Purple";

    public string? EditFolderPath { get; set; }
    public string? PrintFolderPath { get; set; }
}
