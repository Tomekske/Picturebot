using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEditAndPrintFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditFolderPath",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrintFolderPath",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EditFolderPath", "PrintFolderPath" },
                values: new object[] { "", "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditFolderPath",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "PrintFolderPath",
                table: "settings");
        }
    }
}
