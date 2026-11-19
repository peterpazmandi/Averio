using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualReimbursementClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "rule_id",
                table: "reimbursement_claims",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "creation_fingerprint",
                table: "reimbursement_claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $ BEGIN
                    IF EXISTS (SELECT 1 FROM reimbursement_claims WHERE rule_id IS NULL) THEN
                        RAISE EXCEPTION 'Manual reimbursement claims exist. Preserve and resolve them before rolling back this migration.';
                    END IF;
                END $;
                """);
            migrationBuilder.DropColumn(
                name: "creation_fingerprint",
                table: "reimbursement_claims");

            migrationBuilder.AlterColumn<Guid>(
                name: "rule_id",
                table: "reimbursement_claims",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
