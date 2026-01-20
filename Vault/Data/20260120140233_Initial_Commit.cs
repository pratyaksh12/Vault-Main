using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vault.Data
{
    /// <inheritdoc />
    public partial class Initial_Commit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Document",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    project_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<byte>(type: "smallint", nullable: true),
                    extraction_level = table.Column<byte>(type: "smallint", nullable: true),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    extraction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    parent_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    root_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    content_length = table.Column<long>(type: "bigint", nullable: true),
                    charset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ner_mask = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "named_entity",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    mention = table.Column<string>(type: "text", nullable: false),
                    ne_offset = table.Column<long>(type: "bigint", nullable: false),
                    extractor = table.Column<byte>(type: "smallint", nullable: false),
                    category = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    doc_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    root_id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    extractor_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    hidden = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_named_entity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_inventory",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_inventory", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "document_parent_id",
                table: "Document",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "document_status",
                table: "Document",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "named_entity_doc_id",
                table: "named_entity",
                column: "doc_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Document");

            migrationBuilder.DropTable(
                name: "named_entity");

            migrationBuilder.DropTable(
                name: "user_inventory");
        }
    }
}
