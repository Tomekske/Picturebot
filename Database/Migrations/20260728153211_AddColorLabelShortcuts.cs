using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddColorLabelShortcuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlueLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GreenLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NoneLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrangeLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PinkLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PurpleLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RedLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "YellowLabelShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BlueLabelShortcut", "GreenLabelShortcut", "NoneLabelShortcut", "OrangeLabelShortcut", "PinkLabelShortcut", "PurpleLabelShortcut", "RedLabelShortcut", "YellowLabelShortcut" },
                values: new object[] { "Ctrl+NumPad5", "Ctrl+NumPad4", "Ctrl+NumPad0", "Ctrl+NumPad2", "Ctrl+NumPad6", "Ctrl+NumPad7", "Ctrl+NumPad1", "Ctrl+NumPad3" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlueLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "GreenLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "NoneLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "OrangeLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "PinkLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "PurpleLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "RedLabelShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "YellowLabelShortcut",
                table: "settings");
        }
    }
}
