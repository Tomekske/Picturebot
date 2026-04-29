using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBurstSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BurstFallbackTimeThresholdSeconds",
                table: "settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BurstHashSimilarityThreshold",
                table: "settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BurstTimeThresholdSeconds",
                table: "settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "pictures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "pictures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BurstFallbackTimeThresholdSeconds", "BurstHashSimilarityThreshold", "BurstTimeThresholdSeconds", "GroupingThreshold" },
                values: new object[] { 10, 8, 3, 8 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BurstFallbackTimeThresholdSeconds",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "BurstHashSimilarityThreshold",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "BurstTimeThresholdSeconds",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "pictures");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "pictures");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "GroupingThreshold",
                value: 5);
        }
    }
}
