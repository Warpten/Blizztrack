using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blizztrack.Migrations
{
    /// <inheritdoc />
    public partial class AddKnownResourceKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "tact",
                table: "KnownResource",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "tact",
                table: "KnownResource");
        }
    }
}
