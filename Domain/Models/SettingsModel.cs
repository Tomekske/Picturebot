using Domain.Enums;

namespace Domain.Models;

public class SettingsModel {
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    public string? LibraryPath { get; set; }

    public int GroupingThreshold { get; set; } = 10;

    public bool LaunchMaximized { get; set; } = false;
}
