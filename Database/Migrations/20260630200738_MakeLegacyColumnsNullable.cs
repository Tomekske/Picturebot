using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class MakeLegacyColumnsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys=OFF;");
            
            migrationBuilder.Sql(@"
                CREATE TABLE ""pictures_new"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY,
                    ""CapturedAt"" TEXT NOT NULL,
                    ""Extension"" TEXT NULL,
                    ""Hash"" INTEGER NOT NULL,
                    ""Height"" INTEGER NOT NULL,
                    ""LastErrorMessage"" TEXT NULL,
                    ""ProcessingState"" TEXT NOT NULL,
                    ""RetryCount"" INTEGER NOT NULL,
                    ""Sharpness"" INTEGER NOT NULL,
                    ""Width"" INTEGER NOT NULL,
                    ""CurationStatus"" TEXT NULL,
                    ""ColorLabel"" TEXT NULL,
                    ""Rating"" INTEGER NULL,
                    CONSTRAINT ""FK_pictures_nodes_Id"" FOREIGN KEY (""Id"") REFERENCES ""nodes"" (""Id"") ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                INSERT INTO ""pictures_new"" (""Id"", ""CapturedAt"", ""Extension"", ""Hash"", ""Height"", ""LastErrorMessage"", ""ProcessingState"", ""RetryCount"", ""Sharpness"", ""Width"", ""CurationStatus"", ""ColorLabel"", ""Rating"")
                SELECT ""Id"", ""CapturedAt"", ""Extension"", ""Hash"", ""Height"", ""LastErrorMessage"", ""ProcessingState"", ""RetryCount"", ""Sharpness"", ""Width"", ""CurationStatus"", ""ColorLabel"", ""Rating""
                FROM ""pictures"";");

            migrationBuilder.Sql("DROP TABLE \"pictures\";");
            migrationBuilder.Sql("ALTER TABLE \"pictures_new\" RENAME TO \"pictures\";");
            
            migrationBuilder.Sql("PRAGMA foreign_keys=ON;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys=OFF;");
            
            migrationBuilder.Sql(@"
                CREATE TABLE ""pictures_new"" (
                    ""Id"" INTEGER NOT NULL PRIMARY KEY,
                    ""CapturedAt"" TEXT NOT NULL,
                    ""Extension"" TEXT NULL,
                    ""Hash"" INTEGER NOT NULL,
                    ""Height"" INTEGER NOT NULL,
                    ""LastErrorMessage"" TEXT NULL,
                    ""ProcessingState"" TEXT NOT NULL,
                    ""RetryCount"" INTEGER NOT NULL,
                    ""Sharpness"" INTEGER NOT NULL,
                    ""Width"" INTEGER NOT NULL,
                    ""CurationStatus"" TEXT NOT NULL,
                    ""ColorLabel"" TEXT NOT NULL,
                    ""Rating"" INTEGER NOT NULL,
                    CONSTRAINT ""FK_pictures_nodes_Id"" FOREIGN KEY (""Id"") REFERENCES ""nodes"" (""Id"") ON DELETE CASCADE
                );");

            migrationBuilder.Sql(@"
                INSERT INTO ""pictures_new"" (""Id"", ""CapturedAt"", ""Extension"", ""Hash"", ""Height"", ""LastErrorMessage"", ""ProcessingState"", ""RetryCount"", ""Sharpness"", ""Width"", ""CurationStatus"", ""ColorLabel"", ""Rating"")
                SELECT ""Id"", ""CapturedAt"", ""Extension"", ""Hash"", ""Height"", ""LastErrorMessage"", ""ProcessingState"", ""RetryCount"", ""Sharpness"", ""Width"", 
                       COALESCE(""CurationStatus"", 'Unflagged'), 
                       COALESCE(""ColorLabel"", 'None'), 
                       COALESCE(""Rating"", 0)
                FROM ""pictures"";");

            migrationBuilder.Sql("DROP TABLE \"pictures\";");
            migrationBuilder.Sql("ALTER TABLE \"pictures_new\" RENAME TO \"pictures\";");
            
            migrationBuilder.Sql("PRAGMA foreign_keys=ON;");
        }
    }
}
