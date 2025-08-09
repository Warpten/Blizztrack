using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Blizztrack.Migrations
{
    /// <inheritdoc />
    public partial class Initialize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ribbit");

            migrationBuilder.EnsureSchema(
                name: "tact");

            migrationBuilder.CreateTable(
                name: "Endpoint",
                schema: "ribbit",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Regions = table.Column<string[]>(type: "text[]", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    DataPath = table.Column<string>(type: "text", nullable: false),
                    ConfigurationPath = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endpoint", x => x.ID);
                });

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

            migrationBuilder.CreateTable(
                name: "Product",
                schema: "ribbit",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CDN = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BGDL = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EndpointProduct",
                schema: "ribbit",
                columns: table => new
                {
                    EndpointsID = table.Column<int>(type: "integer", nullable: false),
                    ProductsID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointProduct", x => new { x.EndpointsID, x.ProductsID });
                    table.ForeignKey(
                        name: "FK_EndpointProduct_Endpoint_EndpointsID",
                        column: x => x.EndpointsID,
                        principalSchema: "ribbit",
                        principalTable: "Endpoint",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EndpointProduct_Product_ProductsID",
                        column: x => x.ProductsID,
                        principalSchema: "ribbit",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductConfiguration",
                schema: "tact",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildID = table.Column<long>(type: "bigint", nullable: false),
                    ProductID = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DetectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BuildConfig = table.Column<byte[]>(type: "bytea", nullable: false),
                    CDNConfig = table.Column<byte[]>(type: "bytea", nullable: false),
                    KeyRing = table.Column<byte[]>(type: "bytea", nullable: false),
                    Config = table.Column<byte[]>(type: "bytea", nullable: false),
                    Regions = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductConfiguration", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ProductConfiguration_Product_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "ribbit",
                        principalTable: "Product",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Endpoint_Host",
                schema: "ribbit",
                table: "Endpoint",
                column: "Host",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EndpointProduct_ProductsID",
                schema: "ribbit",
                table: "EndpointProduct",
                column: "ProductsID");

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

            migrationBuilder.CreateIndex(
                name: "IX_Product_Code",
                schema: "ribbit",
                table: "Product",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductConfiguration_BuildConfig_CDNConfig_KeyRing_Config_N~",
                schema: "tact",
                table: "ProductConfiguration",
                columns: new[] { "BuildConfig", "CDNConfig", "KeyRing", "Config", "Name", "BuildID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductConfiguration_ProductID",
                schema: "tact",
                table: "ProductConfiguration",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndpointProduct",
                schema: "ribbit");

            migrationBuilder.DropTable(
                name: "KnownResource",
                schema: "tact");

            migrationBuilder.DropTable(
                name: "ProductConfiguration",
                schema: "tact");

            migrationBuilder.DropTable(
                name: "Endpoint",
                schema: "ribbit");

            migrationBuilder.DropTable(
                name: "Product",
                schema: "ribbit");
        }
    }
}
