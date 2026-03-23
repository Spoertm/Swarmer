using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swarmer.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "swarmer");

            migrationBuilder.CreateTable(
                name: "BannedUsers",
                schema: "swarmer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserLogin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameChannels",
                schema: "swarmer",
                columns: table => new
                {
                    TwitchGameId = table.Column<int>(type: "integer", nullable: false),
                    StreamChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameChannels", x => new { x.StreamChannelId, x.TwitchGameId });
                });

            migrationBuilder.CreateTable(
                name: "StreamMessages",
                schema: "swarmer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    StreamId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OfflineThumbnailUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LingeringSinceUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannedUsers_UserLogin",
                schema: "swarmer",
                table: "BannedUsers",
                column: "UserLogin",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannedUsers",
                schema: "swarmer");

            migrationBuilder.DropTable(
                name: "GameChannels",
                schema: "swarmer");

            migrationBuilder.DropTable(
                name: "StreamMessages",
                schema: "swarmer");
        }
    }
}
