using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTagArchitectureToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveTagGroupId",
                table: "settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HierarchyNodesJson",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MasterTagsJson",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TagGroupsJson",
                table: "settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ActiveTagGroupId", "HierarchyNodesJson", "MasterTagsJson", "QuickTagPresets", "TagGroupsJson" },
                values: new object[] { null, "[]", "[]", "", "[]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveTagGroupId",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "HierarchyNodesJson",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "MasterTagsJson",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "TagGroupsJson",
                table: "settings");

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "QuickTagPresets",
                value: "Selected;Review;Highlight;Portrait;Landscape");
        }
    }
}
