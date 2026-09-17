using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCurationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiCurationModel",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnableAiBurstCuration",
                table: "settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GeminiApiKey",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AiFeedback",
                table: "metrics",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AiCurationModel", "EnableAiBurstCuration", "GeminiApiKey" },
                values: new object[] { "gemini-2.5-flash", true, "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiCurationModel",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "EnableAiBurstCuration",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "GeminiApiKey",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "AiFeedback",
                table: "metrics");
        }
    }
}
