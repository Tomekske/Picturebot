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
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    ///     The preferred visual theme mode (e.g., "light", "dark", "system").
    /// </summary>
    [Column("theme_mode")]
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>
    ///     The absolute file system path to the root of the picture library.
    /// </summary>
    [Column("library_path")]
    public string LibraryPath { get; set; } = string.Empty;

    /// <summary>
    ///     The Hamming distance threshold used for grouping similar pictures.
    /// </summary>
    [Column("grouping_threshold")]
    public int GroupingThreshold { get; set; } = 5;

    /// <summary>
    ///     Indicates whether the application should launch in a maximized window state.
    /// </summary>
    [Column("launch_maximized")]
    public bool LaunchMaximized { get; set; } = false;
}
