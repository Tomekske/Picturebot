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

    public string RedLabelShortcut { get; set; } = "Ctrl+NumPad1";
    public string OrangeLabelShortcut { get; set; } = "Ctrl+NumPad2";
    public string YellowLabelShortcut { get; set; } = "Ctrl+NumPad3";
    public string GreenLabelShortcut { get; set; } = "Ctrl+NumPad4";
    public string BlueLabelShortcut { get; set; } = "Ctrl+NumPad5";
    public string PinkLabelShortcut { get; set; } = "Ctrl+NumPad6";
    public string PurpleLabelShortcut { get; set; } = "Ctrl+NumPad7";
    public string NoneLabelShortcut { get; set; } = "Ctrl+NumPad0";

    public string FullscreenShortcut { get; set; } = "F";
    public string OpenInExplorerShortcut { get; set; } = "O";
    public string Rating0Shortcut { get; set; } = "NumPad0";
    public string Rating1Shortcut { get; set; } = "NumPad1";
    public string Rating2Shortcut { get; set; } = "NumPad2";
    public string Rating3Shortcut { get; set; } = "NumPad3";
    public string Rating4Shortcut { get; set; } = "NumPad4";
    public string Rating5Shortcut { get; set; } = "NumPad5";

    public string CurationPickedShortcut { get; set; } = "P";
    public string CurationRejectedShortcut { get; set; } = "X";
    public string CurationNeutralShortcut { get; set; } = "U";

    public string CopyToEditShortcut { get; set; } = "Ctrl+E";
    public string CopyToPrintShortcut { get; set; } = "Shift+E";

    public string? EditFolderPath { get; set; }
    public string? PrintFolderPath { get; set; }
}
