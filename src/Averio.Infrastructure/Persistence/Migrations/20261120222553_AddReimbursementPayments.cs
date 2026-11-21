using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReimbursementPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_claim_status",
                table: "reimbursement_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_claim_submission",
                table: "reimbursement_claims");

            migrationBuilder.CreateTable(
                name: "reimbursement_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    linked_by_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unlinked_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unlinked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reimbursement_payments", x => x.id);
                    table.CheckConstraint("ck_reimbursement_payment_amount", "amount > 0");
                    table.CheckConstraint("ck_reimbursement_payment_unlink", "(unlinked_at IS NULL) = (unlinked_by_member_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_reimbursement_payments_household_members_household_id_linke~",
                        columns: x => new { x.household_id, x.linked_by_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_payments_household_members_household_id_unlin~",
                        columns: x => new { x.household_id, x.unlinked_by_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_payments_reimbursement_claims_household_id_cl~",
                        columns: x => new { x.household_id, x.claim_id },
                        principalTable: "reimbursement_claims",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_payments_transactions_household_id_credit_tra~",
                        columns: x => new { x.household_id, x.credit_transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_claim_status",
                table: "reimbursement_claims",
                sql: "status IN ('NeedsSubmission','Submitted','Rejected','Cancelled','PartiallyReceived','Received')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_claim_submission",
                table: "reimbursement_claims",
                sql: "(status NOT IN ('Submitted','Rejected','PartiallyReceived','Received') OR (submitted_on IS NOT NULL AND expected_on IS NOT NULL)) AND (submitted_on IS NULL OR submitted_on >= payment_on) AND (expected_on IS NULL OR expected_on >= submitted_on) AND payment_delay_days BETWEEN 0 AND 365");

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_credit_transaction_id",
                table: "reimbursement_payments",
                column: "credit_transaction_id",
                unique: true,
                filter: "unlinked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_household_id_claim_id",
                table: "reimbursement_payments",
                columns: new[] { "household_id", "claim_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_household_id_credit_transaction_id",
                table: "reimbursement_payments",
                columns: new[] { "household_id", "credit_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_household_id_linked_by_member_id",
                table: "reimbursement_payments",
                columns: new[] { "household_id", "linked_by_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_household_id_operation_id",
                table: "reimbursement_payments",
                columns: new[] { "household_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_payments_household_id_unlinked_by_member_id",
                table: "reimbursement_payments",
                columns: new[] { "household_id", "unlinked_by_member_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM reimbursement_payments) THEN
                        RAISE EXCEPTION 'Cannot discard reimbursement payment history; restore a consistent backup instead.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "reimbursement_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_claim_status",
                table: "reimbursement_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_claim_submission",
                table: "reimbursement_claims");

            migrationBuilder.AddCheckConstraint(
                name: "ck_claim_status",
                table: "reimbursement_claims",
                sql: "status IN ('NeedsSubmission','Submitted','Rejected','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_claim_submission",
                table: "reimbursement_claims",
                sql: "(status NOT IN ('Submitted','Rejected') OR (submitted_on IS NOT NULL AND expected_on IS NOT NULL)) AND (submitted_on IS NULL OR submitted_on >= payment_on) AND (expected_on IS NULL OR expected_on >= submitted_on) AND payment_delay_days BETWEEN 0 AND 365");
        }
    }
}
