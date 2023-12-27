using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swarmer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameChannels",
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

            migrationBuilder.CreateTable(
                name: "SwarmerConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JsonConfig = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwarmerConfig", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameChannels");

            migrationBuilder.DropTable(
                name: "StreamMessages");

            migrationBuilder.DropTable(
                name: "SwarmerConfig");
        }
    }
}
