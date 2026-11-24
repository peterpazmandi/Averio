using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Averio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AverioDbContext))]
[Migration("20261123113000_AllowPooledReimbursementCredits")]
public sealed class AllowPooledReimbursementCredits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_reimbursement_payments_credit_transaction_id", "reimbursement_payments");
        migrationBuilder.CreateIndex("IX_reimbursement_payments_credit_transaction_id", "reimbursement_payments",
            "credit_transaction_id", filter: "unlinked_at IS NULL");
        migrationBuilder.CreateIndex("IX_reimbursement_payments_claim_id_credit_transaction_id", "reimbursement_payments",
            new[] { "claim_id", "credit_transaction_id" }, unique: true, filter: "unlinked_at IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM reimbursement_payments WHERE unlinked_at IS NULL
                    GROUP BY credit_transaction_id HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Cannot restore exclusive reimbursement credit index while pooled allocations exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex("IX_reimbursement_payments_claim_id_credit_transaction_id", "reimbursement_payments");
        migrationBuilder.DropIndex("IX_reimbursement_payments_credit_transaction_id", "reimbursement_payments");
        migrationBuilder.CreateIndex("IX_reimbursement_payments_credit_transaction_id", "reimbursement_payments",
            "credit_transaction_id", unique: true, filter: "unlinked_at IS NULL");
    }
}

