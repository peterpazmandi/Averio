using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectReimbursementPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reimbursement_privacy_eligible",
                table: "transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM reimbursement_claims) THEN
                        RAISE EXCEPTION 'Cannot remove reimbursement privacy protection while claim history exists.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "reimbursement_privacy_eligible",
                table: "transactions");
        }
    }
}
