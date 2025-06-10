using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Blizztrack.Migrations
{
    /// <inheritdoc />
    public partial class TrackKnownResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnownResource",
                schema: "tact",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncodingKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    Specification = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownResource", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnownResource_ContentKey",
                schema: "tact",
                table: "KnownResource",
                column: "ContentKey")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "IX_KnownResource_EncodingKey",
                schema: "tact",
                table: "KnownResource",
                column: "EncodingKey")
                .Annotation("Npgsql:IndexMethod", "hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownResource",
                schema: "tact");
        }
    }
}
