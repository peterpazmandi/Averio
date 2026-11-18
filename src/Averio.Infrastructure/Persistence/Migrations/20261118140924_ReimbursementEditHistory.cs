using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReimbursementEditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "sequence", table: "reimbursement_claim_events", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "change_kind",
                table: "reimbursement_claim_events",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "destination_account_id",
                table: "reimbursement_claim_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "expected_on",
                table: "reimbursement_claim_events",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_delay_days",
                table: "reimbursement_claim_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "potential_amount",
                table: "reimbursement_claim_events",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "reimbursement_claim_events",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "submission_reminder_on",
                table: "reimbursement_claim_events",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "sequence", table: "reimbursement_claim_events");
            migrationBuilder.DropColumn(
                name: "change_kind",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "destination_account_id",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "expected_on",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "payment_delay_days",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "potential_amount",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "reimbursement_claim_events");

            migrationBuilder.DropColumn(
                name: "submission_reminder_on",
                table: "reimbursement_claim_events");
        }
    }
}
