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
    ///     Indicates whether the application should launch in a maximized window state.
    /// </summary>
    public bool LaunchMaximized { get; set; }

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

    /// <summary>
    ///     The destination folder for files copied for editing.
    /// </summary>
    public string EditFolderPath { get; set; } = string.Empty;

    /// <summary>
    ///     The destination folder for files copied for printing.
    /// </summary>
    public string PrintFolderPath { get; set; } = string.Empty;

    /// <summary>
    ///     The semicolon-separated list of quick tag presets.
    /// </summary>
    public string QuickTagPresets { get; set; } = string.Empty;
    public string GlobalKeywordTaxonomy { get; set; } = string.Empty;

    public string MasterTagsJson { get; set; } = "[]";
    public string HierarchyNodesJson { get; set; } = "[]";
    public string TagGroupsJson { get; set; } = "[]";
    public string? ActiveTagGroupId { get; set; }
}
