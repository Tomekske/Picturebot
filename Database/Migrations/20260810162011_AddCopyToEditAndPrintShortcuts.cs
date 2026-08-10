using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCopyToEditAndPrintShortcuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CopyToEditShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CopyToPrintShortcut",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CopyToEditShortcut", "CopyToPrintShortcut" },
                values: new object[] { "Ctrl+E", "Shift+E" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopyToEditShortcut",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "CopyToPrintShortcut",
                table: "settings");
        }
    }
}
