using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class RMCFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "rank" (
                    "rank_id" INTEGER NOT NULL CONSTRAINT "PK_rank" PRIMARY KEY AUTOINCREMENT,
                    "profile_id" INTEGER NOT NULL,
                    "job_name" TEXT NOT NULL,
                    "rank_name" TEXT NOT NULL,
                    CONSTRAINT "FK_rank_profile_profile_id" FOREIGN KEY ("profile_id") REFERENCES "profile" ("profile_id") ON DELETE CASCADE
                );
                """
            );

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_rank_profile_id" ON "rank" ("profile_id");
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"rank\";");
        }
    }
}
