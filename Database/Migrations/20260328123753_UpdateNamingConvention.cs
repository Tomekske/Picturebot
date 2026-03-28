using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_metrics_pictures_picture_id",
                table: "metrics");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "settings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "theme_mode",
                table: "settings",
                newName: "ThemeMode");

            migrationBuilder.RenameColumn(
                name: "library_path",
                table: "settings",
                newName: "LibraryPath");

            migrationBuilder.RenameColumn(
                name: "launch_maximized",
                table: "settings",
                newName: "LaunchMaximized");

            migrationBuilder.RenameColumn(
                name: "grouping_threshold",
                table: "settings",
                newName: "GroupingThreshold");

            migrationBuilder.RenameColumn(
                name: "curation_status",
                table: "pictures",
                newName: "CurationStatus");

            migrationBuilder.RenameColumn(
                name: "sharpness",
                table: "metrics",
                newName: "Sharpness");

            migrationBuilder.RenameColumn(
                name: "phash",
                table: "metrics",
                newName: "PHash");

            migrationBuilder.RenameColumn(
                name: "picture_id",
                table: "metrics",
                newName: "PictureId");

            migrationBuilder.RenameColumn(
                name: "uuid",
                table: "albums",
                newName: "Uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_metrics_pictures_PictureId",
                table: "metrics",
                column: "PictureId",
                principalTable: "pictures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_metrics_pictures_PictureId",
                table: "metrics");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "settings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ThemeMode",
                table: "settings",
                newName: "theme_mode");

            migrationBuilder.RenameColumn(
                name: "LibraryPath",
                table: "settings",
                newName: "library_path");

            migrationBuilder.RenameColumn(
                name: "LaunchMaximized",
                table: "settings",
                newName: "launch_maximized");

            migrationBuilder.RenameColumn(
                name: "GroupingThreshold",
                table: "settings",
                newName: "grouping_threshold");

            migrationBuilder.RenameColumn(
                name: "CurationStatus",
                table: "pictures",
                newName: "curation_status");

            migrationBuilder.RenameColumn(
                name: "Sharpness",
                table: "metrics",
                newName: "sharpness");

            migrationBuilder.RenameColumn(
                name: "PHash",
                table: "metrics",
                newName: "phash");

            migrationBuilder.RenameColumn(
                name: "PictureId",
                table: "metrics",
                newName: "picture_id");

            migrationBuilder.RenameColumn(
                name: "Uuid",
                table: "albums",
                newName: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_metrics_pictures_picture_id",
                table: "metrics",
                column: "picture_id",
                principalTable: "pictures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
