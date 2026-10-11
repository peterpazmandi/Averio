using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenHouseholdAccountAndInvitationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invitation_acceptance",
                table: "household_invitations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_households_completed_onboarding_profile",
                table: "households",
                sql: "onboarding_completed_at_utc IS NULL\r\nOR (\r\n    name IS NOT NULL\r\n    AND length(btrim(name)) >= 2\r\n    AND country_code IS NOT NULL\r\n    AND preferred_currency IS NOT NULL\r\n)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitation_lifecycle_metadata",
                table: "household_invitations",
                sql: "(\"State\" = 'Pending'\r\n    AND \"AcceptedAt\" IS NULL\r\n    AND \"AcceptedByUserId\" IS NULL\r\n    AND \"SourceHouseholdId\" IS NULL\r\n    AND \"RevokedAt\" IS NULL)\r\n    OR\r\n(\"State\" = 'Accepted'\r\n    AND \"AcceptedAt\" IS NOT NULL\r\n    AND \"AcceptedByUserId\" IS NOT NULL\r\n    AND \"SourceHouseholdId\" IS NOT NULL\r\n    AND \"RevokedAt\" IS NULL)\r\n    OR\r\n(\"State\" = 'Revoked'\r\n    AND \"AcceptedAt\" IS NULL\r\n    AND \"AcceptedByUserId\" IS NULL\r\n    AND \"SourceHouseholdId\" IS NULL\r\n    AND \"RevokedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_archived_at_matches_status",
                table: "accounts",
                sql: "(status = 'Active' AND archived_at IS NULL) OR (status = 'Archived' AND archived_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_households_completed_onboarding_profile",
                table: "households");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitation_lifecycle_metadata",
                table: "household_invitations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_archived_at_matches_status",
                table: "accounts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitation_acceptance",
                table: "household_invitations",
                sql: "(\"State\" = 'Accepted') = (\"AcceptedAt\" IS NOT NULL AND \"AcceptedByUserId\" IS NOT NULL AND \"SourceHouseholdId\" IS NOT NULL)");
        }
    }
}
