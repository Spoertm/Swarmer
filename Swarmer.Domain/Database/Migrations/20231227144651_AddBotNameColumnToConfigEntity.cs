using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarmer.Domain.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBotNameColumnToConfigEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SwarmerConfig",
                table: "SwarmerConfig");

            migrationBuilder.RenameTable(
                name: "SwarmerConfig",
                newName: "BotConfigurations");

            migrationBuilder.AddColumn<string>(
                name: "BotName",
                table: "BotConfigurations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BotConfigurations",
                table: "BotConfigurations",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BotConfigurations",
                table: "BotConfigurations");

            migrationBuilder.DropColumn(
                name: "BotName",
                table: "BotConfigurations");

            migrationBuilder.RenameTable(
                name: "BotConfigurations",
                newName: "SwarmerConfig");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SwarmerConfig",
                table: "SwarmerConfig",
                column: "Id");
        }
    }
}
