using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_scans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    byte_length = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    draft_json = table.Column<string>(type: "character varying(262144)", maxLength: 262144, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    document_deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_scans", x => x.id);
                    table.CheckConstraint("ck_receipt_attempts", "attempts BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_receipt_bytes", "byte_length > 0 AND byte_length <= 10485760");
                    table.CheckConstraint("ck_receipt_draft", "(status = 'Ready') = (draft_json IS NOT NULL)");
                    table.CheckConstraint("ck_receipt_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_receipt_lease", "(status = 'Processing' AND lease_token IS NOT NULL AND lease_until IS NOT NULL) OR (status <> 'Processing' AND lease_token IS NULL AND lease_until IS NULL)");
                    table.CheckConstraint("ck_receipt_status", "status IN ('Uploading','Queued','Processing','Ready','Failed','Discarded')");
                    table.ForeignKey(
                        name: "FK_receipt_scans_household_members_household_id_household_memb~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_expires_at",
                table: "receipt_scans",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_household_id_household_member_id_created_at",
                table: "receipt_scans",
                columns: new[] { "household_id", "household_member_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_household_id_household_member_id_operation_id",
                table: "receipt_scans",
                columns: new[] { "household_id", "household_member_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_status_next_attempt_at",
                table: "receipt_scans",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_scans");
        }
    }
}
