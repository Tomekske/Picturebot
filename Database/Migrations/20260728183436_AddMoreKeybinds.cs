using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreKeybinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullscreenShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpenInExplorerShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating0Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating1Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating2Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating3Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating4Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rating5Shortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FullscreenShortcut", "OpenInExplorerShortcut", "Rating0Shortcut", "Rating1Shortcut", "Rating2Shortcut", "Rating3Shortcut", "Rating4Shortcut", "Rating5Shortcut" },
                values: new object[] { "F", "O", "NumPad0", "NumPad1", "NumPad2", "NumPad3", "NumPad4", "NumPad5" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullscreenShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "OpenInExplorerShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating0Shortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating1Shortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating2Shortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating3Shortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating4Shortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Rating5Shortcut",
                table: "settings");
        }
    }
}
