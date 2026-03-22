using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swarmer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBotConfigAndChangeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotConfigurations");

            migrationBuilder.EnsureSchema(
                name: "swarmer");

            migrationBuilder.RenameTable(
                name: "StreamMessages",
                newName: "StreamMessages",
                newSchema: "swarmer");

            migrationBuilder.RenameTable(
                name: "GameChannels",
                newName: "GameChannels",
                newSchema: "swarmer");

            migrationBuilder.RenameTable(
                name: "BannedUsers",
                newName: "BannedUsers",
                newSchema: "swarmer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "StreamMessages",
                schema: "swarmer",
                newName: "StreamMessages");

            migrationBuilder.RenameTable(
                name: "GameChannels",
                schema: "swarmer",
                newName: "GameChannels");

            migrationBuilder.RenameTable(
                name: "BannedUsers",
                schema: "swarmer",
                newName: "BannedUsers");

            migrationBuilder.CreateTable(
                name: "BotConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BotName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    JsonConfig = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotConfigurations", x => x.Id);
                });
        }
    }
}
