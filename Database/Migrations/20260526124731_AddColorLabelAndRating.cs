using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddColorLabelAndRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlueLabelName",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GreenLabelName",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PurpleLabelName",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RedLabelName",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "YellowLabelName",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ColorLabel",
                table: "pictures",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "pictures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BlueLabelName", "GreenLabelName", "PurpleLabelName", "RedLabelName", "YellowLabelName" },
                values: new object[] { "Blue", "Green", "Purple", "Red", "Yellow" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlueLabelName",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "GreenLabelName",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "PurpleLabelName",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "RedLabelName",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "YellowLabelName",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "ColorLabel",
                table: "pictures");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "pictures");
        }
    }
}
