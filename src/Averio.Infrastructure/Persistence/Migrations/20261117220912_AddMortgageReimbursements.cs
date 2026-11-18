using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMortgageReimbursements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_reimbursement_rules",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    calculation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    maximum_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_delay_days = table.Column<int>(type: "integer", nullable: false),
                    submission_reminder_days = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_reimbursement_rules", x => x.rule_id);
                    table.CheckConstraint("ck_reimbursement_config", "length(btrim(provider)) > 0 AND value > 0 AND calculation IN ('Fixed','Percentage') AND (calculation <> 'Percentage' OR value <= 100) AND (maximum_amount IS NULL OR maximum_amount > 0) AND payment_delay_days BETWEEN 0 AND 365 AND (submission_reminder_days IS NULL OR submission_reminder_days BETWEEN 0 AND 365)");
                    table.ForeignKey(
                        name: "FK_recurring_reimbursement_rules_accounts_destination_account_~",
                        column: x => x.destination_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_reimbursement_rules_recurring_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "recurring_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reimbursement_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    potential_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    original_payment_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    payment_on = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_delay_days = table.Column<int>(type: "integer", nullable: false),
                    submission_reminder_on = table.Column<DateOnly>(type: "date", nullable: true),
                    submitted_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reimbursement_claims", x => x.id);
                    table.UniqueConstraint("AK_reimbursement_claims_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_claim_amount", "potential_amount > 0 AND original_payment_amount >= potential_amount");
                    table.CheckConstraint("ck_claim_currency", "currency IN ('EUR','USD','HUF')");
                    table.CheckConstraint("ck_claim_provider", "length(btrim(provider)) > 0");
                    table.CheckConstraint("ck_claim_status", "status IN ('NeedsSubmission','Submitted','Rejected','Cancelled')");
                    table.CheckConstraint("ck_claim_submission", "(status NOT IN ('Submitted','Rejected') OR (submitted_on IS NOT NULL AND expected_on IS NOT NULL)) AND (submitted_on IS NULL OR submitted_on >= payment_on) AND (expected_on IS NULL OR expected_on >= submitted_on) AND payment_delay_days BETWEEN 0 AND 365");
                    table.ForeignKey(
                        name: "FK_reimbursement_claims_accounts_household_id_destination_acco~",
                        columns: x => new { x.household_id, x.destination_account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_claims_household_members_household_id_househo~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_claims_recurring_rules_household_id_rule_id",
                        columns: x => new { x.household_id, x.rule_id },
                        principalTable: "recurring_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_claims_transactions_household_id_transaction_~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reimbursement_claim_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    submitted_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reimbursement_claim_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_reimbursement_claim_events_household_members_household_id_a~",
                        columns: x => new { x.household_id, x.actor_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reimbursement_claim_events_reimbursement_claims_household_i~",
                        columns: x => new { x.household_id, x.claim_id },
                        principalTable: "reimbursement_claims",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_reimbursement_rules_destination_account_id",
                table: "recurring_reimbursement_rules",
                column: "destination_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claim_events_household_id_actor_member_id",
                table: "reimbursement_claim_events",
                columns: new[] { "household_id", "actor_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claim_events_household_id_claim_id",
                table: "reimbursement_claim_events",
                columns: new[] { "household_id", "claim_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claims_household_id_destination_account_id",
                table: "reimbursement_claims",
                columns: new[] { "household_id", "destination_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claims_household_id_household_member_id_status",
                table: "reimbursement_claims",
                columns: new[] { "household_id", "household_member_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claims_household_id_rule_id",
                table: "reimbursement_claims",
                columns: new[] { "household_id", "rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claims_household_id_transaction_id",
                table: "reimbursement_claims",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reimbursement_claims_transaction_id",
                table: "reimbursement_claims",
                column: "transaction_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_reimbursement_rules");

            migrationBuilder.DropTable(
                name: "reimbursement_claim_events");

            migrationBuilder.DropTable(
                name: "reimbursement_claims");
        }
    }
}
