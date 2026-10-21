using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class STSynthProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "st_synth_profile",
                columns: table => new
                {
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    markings = table.Column<byte[]>(type: "jsonb", nullable: true),
                    hair_name = table.Column<string>(type: "TEXT", nullable: true),
                    hair_color = table.Column<string>(type: "TEXT", nullable: true),
                    facial_hair_name = table.Column<string>(type: "TEXT", nullable: true),
                    facial_hair_color = table.Column<string>(type: "TEXT", nullable: true),
                    eye_color = table.Column<string>(type: "TEXT", nullable: true),
                    skin_color = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_st_synth_profile", x => x.profile_id);
                    table.ForeignKey(
                        name: "FK_st_synth_profile_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "st_synth_profile");
        }
    }
}
