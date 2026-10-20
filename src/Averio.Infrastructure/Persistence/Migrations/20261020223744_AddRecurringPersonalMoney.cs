using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPersonalMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "households",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    country_code = table.Column<string>(type: "char(2)", nullable: true),
                    preferred_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    onboarding_completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    financial_setup_completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
                    table.CheckConstraint("ck_households_completed_onboarding_profile", "onboarding_completed_at_utc IS NULL\nOR (\n    name IS NOT NULL\n    AND length(btrim(name)) >= 2\n    AND country_code IS NOT NULL\n    AND preferred_currency IS NOT NULL\n)");
                    table.CheckConstraint("ck_households_country_code", "country_code IS NULL\nOR country_code ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_households_preferred_currency", "preferred_currency IS NULL\nOR preferred_currency IN ('HUF', 'EUR', 'USD')");
                });

            migrationBuilder.CreateTable(
                name: "icons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_icons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    preferred_culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    visibility = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    include_in_household_reports = table.Column<bool>(type: "boolean", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    balance_anchor_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    balance_anchor_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cutover_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.UniqueConstraint("ak_accounts_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_accounts_archived_at_matches_status", "(status = 'Active' AND archived_at IS NULL) OR (status = 'Archived' AND archived_at IS NOT NULL)");
                    table.CheckConstraint("ck_accounts_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_accounts_scope", "scope IN ('Household', 'Personal')");
                    table.CheckConstraint("ck_accounts_scope_visibility", "(scope = 'Household' AND visibility = 'Household' AND include_in_household_reports)\nOR\n(scope = 'Personal' AND (visibility <> 'Private' OR NOT include_in_household_reports))");
                    table.CheckConstraint("ck_accounts_status", "status IN ('Active', 'Archived')");
                    table.CheckConstraint("ck_accounts_type", "type IN ('Checking', 'Savings', 'Cash', 'CreditCard')");
                    table.CheckConstraint("ck_accounts_visibility", "visibility IN ('Household', 'Private')");
                    table.ForeignKey(
                        name: "fk_accounts_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    system_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.CheckConstraint("ck_categories_accent_color", "accent_color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_categories_kind", "kind IN ('Expense', 'Income', 'Both')");
                    table.CheckConstraint("ck_categories_name_source", "(is_base AND system_key IS NOT NULL AND name IS NULL) OR (NOT is_base AND system_key IS NULL AND name IS NOT NULL)");
                    table.CheckConstraint("ck_categories_not_self_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_categories_scope", "(is_base AND household_id IS NULL) OR (NOT is_base AND household_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_categories_households",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_categories_parent",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    left_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    was_last_member = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_history_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_household_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SendCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceHouseholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_invitations", x => x.Id);
                    table.CheckConstraint("ck_invitation_delivery", "\"DeliveryStatus\" IN ('Unknown','Sent','Failed')");
                    table.CheckConstraint("ck_invitation_expiry", "\"ExpiresAt\" > \"CreatedAt\" AND \"SendCount\" > 0");
                    table.CheckConstraint("ck_invitation_lifecycle_metadata", "(\"State\" = 'Pending'\n    AND \"AcceptedAt\" IS NULL\n    AND \"AcceptedByUserId\" IS NULL\n    AND \"SourceHouseholdId\" IS NULL\n    AND \"RevokedAt\" IS NULL)\n    OR\n(\"State\" = 'Accepted'\n    AND \"AcceptedAt\" IS NOT NULL\n    AND \"AcceptedByUserId\" IS NOT NULL\n    AND \"SourceHouseholdId\" IS NOT NULL\n    AND \"RevokedAt\" IS NULL)\n    OR\n(\"State\" = 'Revoked'\n    AND \"AcceptedAt\" IS NULL\n    AND \"AcceptedByUserId\" IS NULL\n    AND \"SourceHouseholdId\" IS NULL\n    AND \"RevokedAt\" IS NOT NULL)");
                    table.CheckConstraint("ck_invitation_state", "\"State\" IN ('Pending','Accepted','Revoked')");
                    table.ForeignKey(
                        name: "FK_household_invitations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_household_invitations_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_household_invitations_users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_members", x => x.id);
                    table.UniqueConstraint("ak_household_members_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_household_members_role", "role IN ('owner', 'member')");
                    table.ForeignKey(
                        name: "fk_household_members_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.id);
                    table.UniqueConstraint("ak_labels_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_labels_scope", "scope IN ('Household', 'Account')");
                    table.CheckConstraint("ck_labels_scope_account", "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_labels_accounts",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_labels_households",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_aggregates",
                columns: table => new
                {
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    dimension_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    income_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    expense_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    entry_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_aggregates", x => new { x.household_id, x.account_id, x.dimension_type, x.dimension_id, x.currency_code, x.period, x.period_start });
                    table.CheckConstraint("ck_transaction_aggregates_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_transaction_aggregates_dimension", "dimension_type IN ('Account', 'Category')");
                    table.CheckConstraint("ck_transaction_aggregates_non_negative", "income_amount >= 0 AND expense_amount >= 0 AND entry_count >= 0");
                    table.CheckConstraint("ck_transaction_aggregates_period", "period IN ('Day', 'Month', 'Year')");
                    table.ForeignKey(
                        name: "fk_transaction_aggregates_accounts_household_account",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_owners",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_owners", x => new { x.account_id, x.household_member_id });
                    table.ForeignKey(
                        name: "fk_account_owners_accounts",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_account_owners_members",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.id);
                    table.UniqueConstraint("AK_activities_household_id_household_member_id_id", x => new { x.household_id, x.household_member_id, x.id });
                    table.CheckConstraint("ck_activities_dates", "ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on");
                    table.CheckConstraint("ck_activities_name", "length(btrim(name)) > 0");
                    table.ForeignKey(
                        name: "FK_activities_household_members_household_id_household_member_~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_pins",
                columns: table => new
                {
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_pins", x => new { x.household_member_id, x.category_id });
                    table.CheckConstraint("ck_category_pins_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_category_pins_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_category_pins_household_members_household_member_id",
                        column: x => x.household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personal_money_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_money_wallets", x => x.id);
                    table.UniqueConstraint("ak_personal_money_wallet_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_personal_money_wallet_currency", "currency IN ('EUR','USD','HUF')");
                    table.ForeignKey(
                        name: "FK_personal_money_wallets_household_members_household_id_house~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_allowance_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creation_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    creation_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    creation_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    is_stopped = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_allowance_rules", x => x.id);
                    table.UniqueConstraint("AK_recurring_allowance_rules_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_recurring_allowance_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_allowance_currency", "currency IN ('EUR','USD','HUF')");
                    table.CheckConstraint("ck_recurring_allowance_dates", "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 AND (next_due_on IS NULL OR next_due_on >= starts_on)");
                    table.ForeignKey(
                        name: "FK_recurring_allowance_rules_household_members_household_id_re~",
                        columns: x => new { x.household_id, x.recipient_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_transaction_views",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    filters_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_transaction_views", x => x.id);
                    table.CheckConstraint("ck_saved_views_name", "length(btrim(name)) > 0");
                    table.ForeignKey(
                        name: "FK_saved_transaction_views_household_members_household_id_hous~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    merchant = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.UniqueConstraint("ak_transactions_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_transactions_amount", "amount > 0");
                    table.CheckConstraint("ck_transactions_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_transactions_kind", "kind IN ('Income', 'Expense')");
                    table.CheckConstraint("ck_transactions_status", "status IN ('Posted', 'Voided')");
                    table.CheckConstraint("ck_transactions_visibility", "visibility IN ('Household', 'Private')");
                    table.ForeignKey(
                        name: "fk_transactions_accounts_household_account",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_household_members_creator",
                        columns: x => new { x.household_id, x.created_by_household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    destination_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sent_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.id);
                    table.CheckConstraint("ck_transfers_amounts", "sent_amount > 0 AND received_amount > 0");
                    table.CheckConstraint("ck_transfers_currencies", "source_currency IN ('EUR','USD','HUF') AND destination_currency IN ('EUR','USD','HUF')");
                    table.CheckConstraint("ck_transfers_distinct_accounts", "source_account_id <> destination_account_id");
                    table.CheckConstraint("ck_transfers_same_currency", "source_currency <> destination_currency OR sent_amount = received_amount");
                    table.ForeignKey(
                        name: "FK_transfers_accounts_household_id_destination_account_id",
                        columns: x => new { x.household_id, x.destination_account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_accounts_household_id_source_account_id",
                        columns: x => new { x.household_id, x.source_account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_household_members_household_id_created_by_househo~",
                        columns: x => new { x.household_id, x.created_by_household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    automatic = table.Column<bool>(type: "boolean", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    is_stopped = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_rules", x => x.id);
                    table.UniqueConstraint("AK_recurring_rules_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_recurring_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_dates", "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 AND (next_due_on IS NULL OR next_due_on >= starts_on)");
                    table.CheckConstraint("ck_recurring_frequency", "frequency IN ('Daily', 'Weekly', 'Monthly', 'Yearly')");
                    table.CheckConstraint("ck_recurring_kind", "kind IN ('Income', 'Expense')");
                    table.CheckConstraint("ck_recurring_name", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_recurring_visibility", "visibility IN ('Household', 'Private')");
                    table.ForeignKey(
                        name: "FK_recurring_rules_accounts_household_id_account_id",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_rules_activities_household_id_household_member_id~",
                        columns: x => new { x.household_id, x.household_member_id, x.activity_id },
                        principalTable: "activities",
                        principalColumns: new[] { "household_id", "household_member_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_rules_household_members_household_id_household_me~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activity_assignments",
                columns: table => new
                {
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_assignments", x => new { x.household_member_id, x.transaction_id });
                    table.ForeignKey(
                        name: "FK_activity_assignments_activities_household_id_household_memb~",
                        columns: x => new { x.household_id, x.household_member_id, x.activity_id },
                        principalTable: "activities",
                        principalColumns: new[] { "household_id", "household_member_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_assignments_household_members_household_id_househo~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_assignments_transactions_household_id_transaction_~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category_icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_items", x => x.id);
                    table.CheckConstraint("ck_transaction_items_amount", "amount > 0");
                    table.CheckConstraint("ck_transaction_items_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_transaction_items_categories",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_items_transactions",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_labels",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_labels", x => new { x.transaction_id, x.label_id });
                    table.ForeignKey(
                        name: "fk_transaction_labels_labels",
                        columns: x => new { x.household_id, x.label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_labels_transactions",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personal_money_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recurring_allowance_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_expected_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    correction_expected_version = table.Column<int>(type: "integer", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_money_entries", x => x.id);
                    table.CheckConstraint("ck_personal_money_entry_amount", "amount <> 0");
                    table.CheckConstraint("ck_personal_money_entry_kind", "kind IN ('Grant','Funding','PrivateCharge','SharedCharge','PrivateRefund','SharedRefund','Surrender')");
                    table.CheckConstraint("ck_personal_money_entry_source", "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant')");
                    table.ForeignKey(
                        name: "FK_personal_money_entries_personal_money_entries_reverses_entr~",
                        column: x => x.reverses_entry_id,
                        principalTable: "personal_money_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_money_entries_personal_money_wallets_household_id_~",
                        columns: x => new { x.household_id, x.wallet_id },
                        principalTable: "personal_money_wallets",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_money_entries_recurring_allowance_rules_household_~",
                        columns: x => new { x.household_id, x.recurring_allowance_rule_id },
                        principalTable: "recurring_allowance_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_money_entries_transactions_household_id_related_tr~",
                        columns: x => new { x.household_id, x.related_transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_money_entries_transactions_household_id_transactio~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personal_money_entries_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_items", x => x.id);
                    table.CheckConstraint("ck_recurring_item_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_item_description", "length(btrim(description)) > 0 AND position >= 0");
                    table.ForeignKey(
                        name: "FK_recurring_items_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_items_recurring_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "recurring_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_labels",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_labels", x => new { x.rule_id, x.label_id });
                    table.ForeignKey(
                        name: "FK_recurring_labels_labels_household_id_label_id",
                        columns: x => new { x.household_id, x.label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_labels_recurring_rules_household_id_rule_id",
                        columns: x => new { x.household_id, x.rule_id },
                        principalTable: "recurring_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_occurrences",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    item_amounts_snapshot = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_occurrences", x => new { x.rule_id, x.due_on });
                    table.CheckConstraint("ck_recurring_receipt", "(transaction_id IS NULL AND amount IS NULL) OR (transaction_id IS NOT NULL AND amount IS NOT NULL AND amount > 0)");
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_recurring_rules_household_id_rule_id",
                        columns: x => new { x.household_id, x.rule_id },
                        principalTable: "recurring_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_transactions_household_id_transaction~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_owners_household_account",
                table: "account_owners",
                columns: new[] { "household_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_account_owners_household_member",
                table: "account_owners",
                columns: new[] { "household_id", "household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id",
                table: "accounts",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id_status",
                table: "accounts",
                columns: new[] { "household_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id_visibility",
                table: "accounts",
                columns: new[] { "household_id", "visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_activities_household_id_household_member_id_is_closed_name",
                table: "activities",
                columns: new[] { "household_id", "household_member_id", "is_closed", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_assignments_household_id_household_member_id_activ~",
                table: "activity_assignments",
                columns: new[] { "household_id", "household_member_id", "activity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_assignments_household_id_transaction_id",
                table: "activity_assignments",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_base_kind_status",
                table: "categories",
                columns: new[] { "is_base", "kind", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_household_parent_order",
                table: "categories",
                columns: new[] { "household_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_categories_system_key",
                table: "categories",
                column: "system_key",
                unique: true,
                filter: "is_base");

            migrationBuilder.CreateIndex(
                name: "IX_category_pins_category_id",
                table: "category_pins",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_history_household_id",
                table: "household_history",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_history_user_id_left_at_utc",
                table: "household_history",
                columns: new[] { "user_id", "left_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_AcceptedByUserId",
                table: "household_invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_HouseholdId",
                table: "household_invitations",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_HouseholdId_NormalizedEmail",
                table: "household_invitations",
                columns: new[] { "HouseholdId", "NormalizedEmail" },
                unique: true,
                filter: "\"State\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_InviterUserId",
                table: "household_invitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_TokenHash",
                table: "household_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_household_members_household_id",
                table: "household_members",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_household_owner",
                table: "household_members",
                column: "household_id",
                unique: true,
                filter: "role = 'owner' AND left_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_user_id",
                table: "household_members",
                column: "user_id",
                unique: true,
                filter: "left_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_icons_sort_order",
                table: "icons",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ux_icons_value",
                table: "icons",
                column: "value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labels_scope_account",
                table: "labels",
                columns: new[] { "household_id", "scope", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_labels_account_name",
                table: "labels",
                columns: new[] { "household_id", "account_id", "normalized_name" },
                unique: true,
                filter: "scope = 'Account'");

            migrationBuilder.CreateIndex(
                name: "ux_labels_household_name",
                table: "labels",
                columns: new[] { "household_id", "normalized_name" },
                unique: true,
                filter: "scope = 'Household'");

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_recurring_allowance_rul~",
                table: "personal_money_entries",
                columns: new[] { "household_id", "recurring_allowance_rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_related_transaction_id",
                table: "personal_money_entries",
                columns: new[] { "household_id", "related_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_transaction_id",
                table: "personal_money_entries",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_transfer_id",
                table: "personal_money_entries",
                columns: new[] { "household_id", "transfer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_wallet_id",
                table: "personal_money_entries",
                columns: new[] { "household_id", "wallet_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_recurring_allowance_rule_id_occurred~",
                table: "personal_money_entries",
                columns: new[] { "recurring_allowance_rule_id", "occurred_on" },
                unique: true,
                filter: "recurring_allowance_rule_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_reverses_entry_id",
                table: "personal_money_entries",
                column: "reverses_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_transfer_id",
                table: "personal_money_entries",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_wallet_id_occurred_on_recorded_at",
                table: "personal_money_entries",
                columns: new[] { "wallet_id", "occurred_on", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_wallet_id_operation_id",
                table: "personal_money_entries",
                columns: new[] { "wallet_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_wallets_household_id_household_member_id",
                table: "personal_money_wallets",
                columns: new[] { "household_id", "household_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_household_id_creation_operation_id",
                table: "recurring_allowance_rules",
                columns: new[] { "household_id", "creation_operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_household_id_recipient_member_id",
                table: "recurring_allowance_rules",
                columns: new[] { "household_id", "recipient_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_next_due_on_id",
                table: "recurring_allowance_rules",
                columns: new[] { "next_due_on", "id" },
                filter: "NOT is_paused AND NOT is_stopped AND NOT is_blocked AND next_due_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_items_category_id",
                table: "recurring_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_items_rule_id_position",
                table: "recurring_items",
                columns: new[] { "rule_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_labels_household_id_label_id",
                table: "recurring_labels",
                columns: new[] { "household_id", "label_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_labels_household_id_rule_id",
                table: "recurring_labels",
                columns: new[] { "household_id", "rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_household_id_rule_id",
                table: "recurring_occurrences",
                columns: new[] { "household_id", "rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_household_id_transaction_id",
                table: "recurring_occurrences",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_transaction_id",
                table: "recurring_occurrences",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_category_id",
                table: "recurring_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_account_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_household_member_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_household_member_id_activity_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "household_member_id", "activity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_next_due_on_id",
                table: "recurring_rules",
                columns: new[] { "next_due_on", "id" },
                filter: "automatic AND NOT is_paused AND NOT is_stopped AND NOT is_blocked");

            migrationBuilder.CreateIndex(
                name: "IX_saved_transaction_views_household_id_household_member_id_no~",
                table: "saved_transaction_views",
                columns: new[] { "household_id", "household_member_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_dimension",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "dimension_type", "dimension_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_household_dimension_period",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "dimension_type", "period", "period_start", "currency_code" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_items_category",
                table: "transaction_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_transaction_items_transaction_position",
                table: "transaction_items",
                columns: new[] { "household_id", "transaction_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_labels_household_id_transaction_id",
                table: "transaction_labels",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_labels_label",
                table: "transaction_labels",
                columns: new[] { "household_id", "label_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_account_date",
                table: "transactions",
                columns: new[] { "household_id", "account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_date_created",
                table: "transactions",
                columns: new[] { "household_id", "occurred_on", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_household_id_created_by_household_member_id",
                table: "transactions",
                columns: new[] { "household_id", "created_by_household_member_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_status_date",
                table: "transactions",
                columns: new[] { "household_id", "status", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_visibility_date",
                table: "transactions",
                columns: new[] { "household_id", "visibility", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_created_by_household_member_id",
                table: "transfers",
                columns: new[] { "household_id", "created_by_household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_destination_account_id_occurred_on",
                table: "transfers",
                columns: new[] { "household_id", "destination_account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_occurred_on_created_at_id",
                table: "transfers",
                columns: new[] { "household_id", "occurred_on", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_source_account_id_occurred_on",
                table: "transfers",
                columns: new[] { "household_id", "source_account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_owners");

            migrationBuilder.DropTable(
                name: "activity_assignments");

            migrationBuilder.DropTable(
                name: "category_pins");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "household_history");

            migrationBuilder.DropTable(
                name: "household_invitations");

            migrationBuilder.DropTable(
                name: "icons");

            migrationBuilder.DropTable(
                name: "personal_money_entries");

            migrationBuilder.DropTable(
                name: "recurring_items");

            migrationBuilder.DropTable(
                name: "recurring_labels");

            migrationBuilder.DropTable(
                name: "recurring_occurrences");

            migrationBuilder.DropTable(
                name: "saved_transaction_views");

            migrationBuilder.DropTable(
                name: "transaction_aggregates");

            migrationBuilder.DropTable(
                name: "transaction_items");

            migrationBuilder.DropTable(
                name: "transaction_labels");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "personal_money_wallets");

            migrationBuilder.DropTable(
                name: "recurring_allowance_rules");

            migrationBuilder.DropTable(
                name: "transfers");

            migrationBuilder.DropTable(
                name: "recurring_rules");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "household_members");

            migrationBuilder.DropTable(
                name: "households");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
