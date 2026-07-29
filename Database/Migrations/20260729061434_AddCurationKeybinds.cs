using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCurationKeybinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurationNeutralShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurationPickedShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurationRejectedShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CurationNeutralShortcut", "CurationPickedShortcut", "CurationRejectedShortcut" },
                values: new object[] { "U", "P", "X" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurationNeutralShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "CurationPickedShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "CurationRejectedShortcut",
                table: "settings");
        }
    }
}
