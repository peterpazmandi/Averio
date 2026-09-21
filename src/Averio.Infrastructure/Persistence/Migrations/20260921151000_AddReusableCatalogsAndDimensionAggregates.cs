using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

public partial class AddReusableCatalogsAndDimensionAggregates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
            name: "categories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: true),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                table.CheckConstraint("ck_categories_kind", "kind IN ('Expense', 'Income', 'Both')");
                table.CheckConstraint("ck_categories_scope", "(is_base AND household_id IS NULL) OR (NOT is_base AND household_id IS NOT NULL)");
                table.CheckConstraint("ck_categories_accent_color", "accent_color ~ '^#[0-9A-Fa-f]{6}$'");
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

        migrationBuilder.CreateIndex(
            name: "ux_icons_value",
            table: "icons",
            column: "value",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_icons_sort_order",
            table: "icons",
            column: "sort_order");

        migrationBuilder.CreateIndex(
            name: "ux_categories_base_code",
            table: "categories",
            column: "code",
            unique: true,
            filter: "is_base");

        migrationBuilder.CreateIndex(
            name: "ix_categories_household_parent_order",
            table: "categories",
            columns: new[] { "household_id", "parent_id", "sort_order" });

        migrationBuilder.CreateIndex(
            name: "ix_categories_base_kind_status",
            table: "categories",
            columns: new[] { "is_base", "kind", "is_archived" });

        migrationBuilder.Sql(
            """
            INSERT INTO icons (id, name, value, sort_order)
            SELECT
                ordinality::int,
                'icon-' || lpad(ordinality::text, 3, '0'),
                value,
                ordinality::int
            FROM unnest(ARRAY['🏠', '🏡', '🏢', '🏬', '🏦', '🏥', '🏨', '🏫', '🏭', '🏰', '🏘️', '🛖', '🏗️', '🧱', '🚪', '🪟', '🛋️', '🛏️', '🪑', '🛁', '🛒', '🛍️', '🧺', '🧾', '💳', '💵', '💶', '💷', '💴', '🪙', '💰', '🏧', '🧮', '📦', '🎁', '🏷️', '🛎️', '🧰', '🔧', '🚗', '🚕', '🚌', '🚎', '🏎️', '🚓', '🚑', '🚒', '🚐', '🛻', '🚚', '🚛', '🚜', '🏍️', '🛵', '🚲', '🛴', '🚆', '🚇', '✈️', '⛽', '🅿️', '🚦', '🛣️', '🧭', '🗺️', '🧳', '🛫', '🛬', '🚢', '⛵', '🚤', '⚓', '🚁', '🚀', '🛞', '🔋', '🧯', '🦺', '🛠️', '🍎', '🍌', '🍇', '🍓', '🍒', '🍉', '🍋', '🍊', '🍐', '🥝', '🍅', '🥕', '🌽', '🥦', '🥬', '🥔', '🧅', '🍞', '🥐', '🥖', '🧀', '🥚', '🥛', '☕', '🫖', '🧃', '🥤', '🍺', '🍷', '🍽️', '🍴', '🥄', '🥢', '🍳', '🍲', '🍝', '🍕', '🍔', '🌭', '🌮', '👶', '🧒', '👧', '👦', '🎒', '✏️', '📚', '📖', '🎓', '🧸', '🪁', '⚽', '🏀', '🏊', '🚴', '🎨', '🎹', '🎮', '🧩', '🎯', '❤️', '🩺', '💊', '💉', '🩹', '🧬', '🦷', '👓', '🧠', '🫀', '🫁', '🧴', '🧼', '🪥', '💇', '🧘', '🏋️', '🏃', '🚶', '🛌', '🐶', '🐱', '🐭', '🐹', '🐰', '🦊', '🐻', '🐼', '🐨', '🐯', '🦁', '🐮', '🐷', '🐸', '🐵', '🐔', '🐟', '🐠', '🐾', '🦴', '💡', '🔌', '🔦', '🕯️', '🔥', '💧', '🚿', '📱', '☎️', '💻', '🖥️', '🖨️', '📺', '📻', '🎧', '📷', '🔒', '🔑', '🛡️', '⚙️', '🎬', '🎵', '🎤', '🎟️', '🎫', '🎪', '🎭', '🎲', '♟️', '🃏', '🎳', '⛳', '🏆', '🥇', '🎣', '🌴', '🏖️', '🏝️', '⛰️', '🏕️', '🏞️', '🗻', '🌄', '🌅', '🌇', '🌃', '🌍', '🌎', '🌏', '☀️', '🌤️', '🌧️', '❄️', '🌈', '🌊', '📈', '📉', '💹', '💼', '🧑‍💼', '👔']::text[]) WITH ORDINALITY AS seed(value, ordinality);
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO categories
                (id, household_id, parent_id, code, name, kind, icon, accent_color,
                 sort_order, is_base, is_archived, created_at, updated_at)
            VALUES
            ('00000000-0000-4000-8000-000000000001'::uuid, NULL, NULL, 'uncategorized', 'Uncategorized', 'Both', '🗂️', '#10B981', 10, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000002'::uuid, NULL, '00000000-0000-4000-8000-000000000001'::uuid, 'uncategorized-miscellaneous', 'Miscellaneous', 'Both', '📦', '#10B981', 20, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000003'::uuid, NULL, '00000000-0000-4000-8000-000000000001'::uuid, 'uncategorized-cash-adjustment', 'Cash adjustment', 'Both', '🧮', '#10B981', 30, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000004'::uuid, NULL, '00000000-0000-4000-8000-000000000001'::uuid, 'uncategorized-other-income', 'Other income', 'Income', '➕', '#10B981', 40, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000005'::uuid, NULL, '00000000-0000-4000-8000-000000000001'::uuid, 'uncategorized-other-expense', 'Other expense', 'Expense', '➖', '#10B981', 50, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000006'::uuid, NULL, NULL, 'housing', 'Housing', 'Expense', '🏠', '#6366F1', 60, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000007'::uuid, NULL, '00000000-0000-4000-8000-000000000006'::uuid, 'housing-mortgage-rent', 'Mortgage / rent', 'Expense', '🏠', '#6366F1', 70, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000008'::uuid, NULL, '00000000-0000-4000-8000-000000000006'::uuid, 'housing-home-maintenance', 'Home maintenance', 'Expense', '🔧', '#6366F1', 80, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000009'::uuid, NULL, '00000000-0000-4000-8000-000000000006'::uuid, 'housing-furniture', 'Furniture', 'Expense', '🛋️', '#6366F1', 90, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000010'::uuid, NULL, '00000000-0000-4000-8000-000000000006'::uuid, 'housing-property-fees', 'Property fees', 'Expense', '🧾', '#6366F1', 100, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000011'::uuid, NULL, NULL, 'groceries', 'Groceries', 'Expense', '🛒', '#0EA5E9', 110, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000012'::uuid, NULL, '00000000-0000-4000-8000-000000000011'::uuid, 'groceries-supermarket', 'Supermarket', 'Expense', '🛒', '#0EA5E9', 120, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000013'::uuid, NULL, '00000000-0000-4000-8000-000000000011'::uuid, 'groceries-bakery', 'Bakery', 'Expense', '🥐', '#0EA5E9', 130, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000014'::uuid, NULL, '00000000-0000-4000-8000-000000000011'::uuid, 'groceries-market', 'Market', 'Expense', '🧺', '#0EA5E9', 140, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000015'::uuid, NULL, '00000000-0000-4000-8000-000000000011'::uuid, 'groceries-household-groceries', 'Household groceries', 'Expense', '🍞', '#0EA5E9', 150, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000016'::uuid, NULL, NULL, 'dining', 'Dining', 'Expense', '🍽️', '#F59E0B', 160, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000017'::uuid, NULL, '00000000-0000-4000-8000-000000000016'::uuid, 'dining-restaurants', 'Restaurants', 'Expense', '🍽️', '#F59E0B', 170, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000018'::uuid, NULL, '00000000-0000-4000-8000-000000000016'::uuid, 'dining-fast-food', 'Fast food', 'Expense', '🍔', '#F59E0B', 180, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000019'::uuid, NULL, '00000000-0000-4000-8000-000000000016'::uuid, 'dining-coffee-and-tea', 'Coffee & tea', 'Expense', '☕', '#F59E0B', 190, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000020'::uuid, NULL, '00000000-0000-4000-8000-000000000016'::uuid, 'dining-takeaway', 'Takeaway', 'Expense', '🥡', '#F59E0B', 200, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000021'::uuid, NULL, NULL, 'transport', 'Transport', 'Expense', '🚗', '#EC4899', 210, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000022'::uuid, NULL, '00000000-0000-4000-8000-000000000021'::uuid, 'transport-fuel', 'Fuel', 'Expense', '⛽', '#EC4899', 220, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000023'::uuid, NULL, '00000000-0000-4000-8000-000000000021'::uuid, 'transport-public-transport', 'Public transport', 'Expense', '🚌', '#EC4899', 230, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000024'::uuid, NULL, '00000000-0000-4000-8000-000000000021'::uuid, 'transport-parking', 'Parking', 'Expense', '🅿️', '#EC4899', 240, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000025'::uuid, NULL, '00000000-0000-4000-8000-000000000021'::uuid, 'transport-repairs-and-maintenance', 'Repairs & maintenance', 'Expense', '🛠️', '#EC4899', 250, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000026'::uuid, NULL, NULL, 'utilities', 'Utilities', 'Expense', '💡', '#64748B', 260, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000027'::uuid, NULL, '00000000-0000-4000-8000-000000000026'::uuid, 'utilities-electricity', 'Electricity', 'Expense', '⚡', '#64748B', 270, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000028'::uuid, NULL, '00000000-0000-4000-8000-000000000026'::uuid, 'utilities-gas', 'Gas', 'Expense', '🔥', '#64748B', 280, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000029'::uuid, NULL, '00000000-0000-4000-8000-000000000026'::uuid, 'utilities-water', 'Water', 'Expense', '💧', '#64748B', 290, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000030'::uuid, NULL, '00000000-0000-4000-8000-000000000026'::uuid, 'utilities-internet-and-tv', 'Internet & TV', 'Expense', '📺', '#64748B', 300, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000031'::uuid, NULL, NULL, 'children', 'Children', 'Expense', '👶', '#10B981', 310, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000032'::uuid, NULL, '00000000-0000-4000-8000-000000000031'::uuid, 'children-childcare', 'Childcare', 'Expense', '🧸', '#10B981', 320, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000033'::uuid, NULL, '00000000-0000-4000-8000-000000000031'::uuid, 'children-school', 'School', 'Expense', '🎒', '#10B981', 330, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000034'::uuid, NULL, '00000000-0000-4000-8000-000000000031'::uuid, 'children-clothing', 'Clothing', 'Expense', '👕', '#10B981', 340, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000035'::uuid, NULL, '00000000-0000-4000-8000-000000000031'::uuid, 'children-activities', 'Activities', 'Expense', '⚽', '#10B981', 350, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000036'::uuid, NULL, NULL, 'health', 'Health', 'Expense', '❤️', '#6366F1', 360, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000037'::uuid, NULL, '00000000-0000-4000-8000-000000000036'::uuid, 'health-doctor', 'Doctor', 'Expense', '🩺', '#6366F1', 370, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000038'::uuid, NULL, '00000000-0000-4000-8000-000000000036'::uuid, 'health-pharmacy', 'Pharmacy', 'Expense', '💊', '#6366F1', 380, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000039'::uuid, NULL, '00000000-0000-4000-8000-000000000036'::uuid, 'health-dental', 'Dental', 'Expense', '🦷', '#6366F1', 390, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000040'::uuid, NULL, '00000000-0000-4000-8000-000000000036'::uuid, 'health-therapy', 'Therapy', 'Expense', '🧠', '#6366F1', 400, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000041'::uuid, NULL, NULL, 'insurance', 'Insurance', 'Expense', '🛡️', '#0EA5E9', 410, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000042'::uuid, NULL, '00000000-0000-4000-8000-000000000041'::uuid, 'insurance-home-insurance', 'Home insurance', 'Expense', '🏠', '#0EA5E9', 420, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000043'::uuid, NULL, '00000000-0000-4000-8000-000000000041'::uuid, 'insurance-car-insurance', 'Car insurance', 'Expense', '🚗', '#0EA5E9', 430, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000044'::uuid, NULL, '00000000-0000-4000-8000-000000000041'::uuid, 'insurance-health-insurance', 'Health insurance', 'Expense', '❤️', '#0EA5E9', 440, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000045'::uuid, NULL, '00000000-0000-4000-8000-000000000041'::uuid, 'insurance-life-insurance', 'Life insurance', 'Expense', '🛡️', '#0EA5E9', 450, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000046'::uuid, NULL, NULL, 'debt', 'Debt', 'Expense', '💳', '#F59E0B', 460, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000047'::uuid, NULL, '00000000-0000-4000-8000-000000000046'::uuid, 'debt-loan-payment', 'Loan payment', 'Expense', '🏦', '#F59E0B', 470, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000048'::uuid, NULL, '00000000-0000-4000-8000-000000000046'::uuid, 'debt-credit-card-interest', 'Credit card interest', 'Expense', '💳', '#F59E0B', 480, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000049'::uuid, NULL, '00000000-0000-4000-8000-000000000046'::uuid, 'debt-bank-overdraft', 'Bank overdraft', 'Expense', '🏧', '#F59E0B', 490, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000050'::uuid, NULL, '00000000-0000-4000-8000-000000000046'::uuid, 'debt-other-debt', 'Other debt', 'Expense', '📉', '#F59E0B', 500, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000051'::uuid, NULL, NULL, 'education', 'Education', 'Expense', '🎓', '#EC4899', 510, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000052'::uuid, NULL, '00000000-0000-4000-8000-000000000051'::uuid, 'education-courses', 'Courses', 'Expense', '🎓', '#EC4899', 520, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000053'::uuid, NULL, '00000000-0000-4000-8000-000000000051'::uuid, 'education-books', 'Books', 'Expense', '📚', '#EC4899', 530, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000054'::uuid, NULL, '00000000-0000-4000-8000-000000000051'::uuid, 'education-school-fees', 'School fees', 'Expense', '🧾', '#EC4899', 540, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000055'::uuid, NULL, '00000000-0000-4000-8000-000000000051'::uuid, 'education-learning-tools', 'Learning tools', 'Expense', '💻', '#EC4899', 550, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000056'::uuid, NULL, NULL, 'shopping', 'Shopping', 'Expense', '🛍️', '#64748B', 560, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000057'::uuid, NULL, '00000000-0000-4000-8000-000000000056'::uuid, 'shopping-clothing', 'Clothing', 'Expense', '👕', '#64748B', 570, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000058'::uuid, NULL, '00000000-0000-4000-8000-000000000056'::uuid, 'shopping-electronics', 'Electronics', 'Expense', '📱', '#64748B', 580, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000059'::uuid, NULL, '00000000-0000-4000-8000-000000000056'::uuid, 'shopping-home-goods', 'Home goods', 'Expense', '🏡', '#64748B', 590, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000060'::uuid, NULL, '00000000-0000-4000-8000-000000000056'::uuid, 'shopping-online-shopping', 'Online shopping', 'Expense', '📦', '#64748B', 600, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000061'::uuid, NULL, NULL, 'personal-care', 'Personal care', 'Expense', '🧴', '#10B981', 610, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000062'::uuid, NULL, '00000000-0000-4000-8000-000000000061'::uuid, 'personal-care-hairdresser', 'Hairdresser', 'Expense', '💇', '#10B981', 620, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000063'::uuid, NULL, '00000000-0000-4000-8000-000000000061'::uuid, 'personal-care-cosmetics', 'Cosmetics', 'Expense', '💄', '#10B981', 630, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000064'::uuid, NULL, '00000000-0000-4000-8000-000000000061'::uuid, 'personal-care-gym-and-fitness', 'Gym & fitness', 'Expense', '🏋️', '#10B981', 640, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000065'::uuid, NULL, '00000000-0000-4000-8000-000000000061'::uuid, 'personal-care-wellness', 'Wellness', 'Expense', '🧘', '#10B981', 650, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000066'::uuid, NULL, NULL, 'entertainment', 'Entertainment', 'Expense', '🎬', '#6366F1', 660, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000067'::uuid, NULL, '00000000-0000-4000-8000-000000000066'::uuid, 'entertainment-streaming', 'Streaming', 'Expense', '📺', '#6366F1', 670, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000068'::uuid, NULL, '00000000-0000-4000-8000-000000000066'::uuid, 'entertainment-games', 'Games', 'Expense', '🎮', '#6366F1', 680, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000069'::uuid, NULL, '00000000-0000-4000-8000-000000000066'::uuid, 'entertainment-events', 'Events', 'Expense', '🎟️', '#6366F1', 690, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000070'::uuid, NULL, '00000000-0000-4000-8000-000000000066'::uuid, 'entertainment-hobbies', 'Hobbies', 'Expense', '🎨', '#6366F1', 700, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000071'::uuid, NULL, NULL, 'travel', 'Travel', 'Expense', '✈️', '#0EA5E9', 710, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000072'::uuid, NULL, '00000000-0000-4000-8000-000000000071'::uuid, 'travel-flights', 'Flights', 'Expense', '✈️', '#0EA5E9', 720, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000073'::uuid, NULL, '00000000-0000-4000-8000-000000000071'::uuid, 'travel-accommodation', 'Accommodation', 'Expense', '🏨', '#0EA5E9', 730, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000074'::uuid, NULL, '00000000-0000-4000-8000-000000000071'::uuid, 'travel-local-transport', 'Local transport', 'Expense', '🚇', '#0EA5E9', 740, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000075'::uuid, NULL, '00000000-0000-4000-8000-000000000071'::uuid, 'travel-travel-food', 'Travel food', 'Expense', '🍴', '#0EA5E9', 750, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000076'::uuid, NULL, NULL, 'pets', 'Pets', 'Expense', '🐾', '#F59E0B', 760, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000077'::uuid, NULL, '00000000-0000-4000-8000-000000000076'::uuid, 'pets-pet-food', 'Pet food', 'Expense', '🐶', '#F59E0B', 770, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000078'::uuid, NULL, '00000000-0000-4000-8000-000000000076'::uuid, 'pets-veterinary', 'Veterinary', 'Expense', '🩺', '#F59E0B', 780, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000079'::uuid, NULL, '00000000-0000-4000-8000-000000000076'::uuid, 'pets-grooming', 'Grooming', 'Expense', '🧼', '#F59E0B', 790, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000080'::uuid, NULL, '00000000-0000-4000-8000-000000000076'::uuid, 'pets-pet-supplies', 'Pet supplies', 'Expense', '🐾', '#F59E0B', 800, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000081'::uuid, NULL, NULL, 'gifts-and-donations', 'Gifts & donations', 'Expense', '🎁', '#EC4899', 810, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000082'::uuid, NULL, '00000000-0000-4000-8000-000000000081'::uuid, 'gifts-and-donations-gifts', 'Gifts', 'Expense', '🎁', '#EC4899', 820, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000083'::uuid, NULL, '00000000-0000-4000-8000-000000000081'::uuid, 'gifts-and-donations-charity', 'Charity', 'Expense', '🤲', '#EC4899', 830, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000084'::uuid, NULL, '00000000-0000-4000-8000-000000000081'::uuid, 'gifts-and-donations-celebrations', 'Celebrations', 'Expense', '🎉', '#EC4899', 840, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000085'::uuid, NULL, '00000000-0000-4000-8000-000000000081'::uuid, 'gifts-and-donations-religious-giving', 'Religious giving', 'Expense', '🙏', '#EC4899', 850, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000086'::uuid, NULL, NULL, 'taxes-and-fees', 'Taxes & fees', 'Expense', '🧾', '#64748B', 860, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000087'::uuid, NULL, '00000000-0000-4000-8000-000000000086'::uuid, 'taxes-and-fees-taxes', 'Taxes', 'Expense', '🏛️', '#64748B', 870, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000088'::uuid, NULL, '00000000-0000-4000-8000-000000000086'::uuid, 'taxes-and-fees-bank-fees', 'Bank fees', 'Expense', '🏦', '#64748B', 880, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000089'::uuid, NULL, '00000000-0000-4000-8000-000000000086'::uuid, 'taxes-and-fees-government-fees', 'Government fees', 'Expense', '📄', '#64748B', 890, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000090'::uuid, NULL, '00000000-0000-4000-8000-000000000086'::uuid, 'taxes-and-fees-fines', 'Fines', 'Expense', '⚖️', '#64748B', 900, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000091'::uuid, NULL, NULL, 'income', 'Income', 'Income', '💰', '#10B981', 910, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000092'::uuid, NULL, '00000000-0000-4000-8000-000000000091'::uuid, 'income-salary', 'Salary', 'Income', '💼', '#10B981', 920, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000093'::uuid, NULL, '00000000-0000-4000-8000-000000000091'::uuid, 'income-bonus', 'Bonus', 'Income', '⭐', '#10B981', 930, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000094'::uuid, NULL, '00000000-0000-4000-8000-000000000091'::uuid, 'income-freelance', 'Freelance', 'Income', '💻', '#10B981', 940, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000095'::uuid, NULL, '00000000-0000-4000-8000-000000000091'::uuid, 'income-benefits', 'Benefits', 'Income', '🤝', '#10B981', 950, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000096'::uuid, NULL, NULL, 'investments-and-returns', 'Investments & returns', 'Income', '📈', '#6366F1', 960, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000097'::uuid, NULL, '00000000-0000-4000-8000-000000000096'::uuid, 'investments-and-returns-interest', 'Interest', 'Income', '🏦', '#6366F1', 970, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000098'::uuid, NULL, '00000000-0000-4000-8000-000000000096'::uuid, 'investments-and-returns-dividends', 'Dividends', 'Income', '💹', '#6366F1', 980, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000099'::uuid, NULL, '00000000-0000-4000-8000-000000000096'::uuid, 'investments-and-returns-investment-gains', 'Investment gains', 'Income', '📈', '#6366F1', 990, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00'),
('00000000-0000-4000-8000-000000000100'::uuid, NULL, '00000000-0000-4000-8000-000000000096'::uuid, 'investments-and-returns-refunds', 'Refunds', 'Income', '↩️', '#6366F1', 1000, TRUE, FALSE, TIMESTAMPTZ '2026-09-21 00:00:00+00', TIMESTAMPTZ '2026-09-21 00:00:00+00');
            """);

        migrationBuilder.AddColumn<Guid>(
            name: "category_id",
            table: "transaction_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "category_icon",
            table: "transaction_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "category_name",
            table: "transaction_items",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80,
            oldNullable: true);

        migrationBuilder.Sql(
            """
            UPDATE transaction_items AS item
            SET
                category_id = COALESCE(matched.id, '00000000-0000-4000-8000-000000000001'::uuid),
                category_name = COALESCE(NULLIF(item.category_name, ''), matched.name, 'Uncategorized'),
                category_icon = COALESCE(matched.icon, '🗂️')
            FROM LATERAL (
                SELECT category.id, category.name, category.icon
                FROM categories AS category
                WHERE category.is_base
                  AND lower(category.name) = lower(COALESCE(item.category_name, ''))
                ORDER BY category.sort_order
                LIMIT 1
            ) AS matched
            WHERE TRUE;
            """);

        migrationBuilder.Sql(
            """
            UPDATE transaction_items
            SET
                category_id = '00000000-0000-4000-8000-000000000001'::uuid,
                category_name = COALESCE(NULLIF(category_name, ''), 'Uncategorized'),
                category_icon = COALESCE(NULLIF(category_icon, ''), '🗂️')
            WHERE category_id IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "category_id",
            table: "transaction_items",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "category_name",
            table: "transaction_items",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "category_icon",
            table: "transaction_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_transaction_items_category",
            table: "transaction_items",
            column: "category_id");

        migrationBuilder.AddForeignKey(
            name: "fk_transaction_items_categories",
            table: "transaction_items",
            column: "category_id",
            principalTable: "categories",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.RenameTable(
            name: "transaction_labels",
            newName: "transaction_labels_legacy");

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
                table.CheckConstraint(
                    "ck_labels_scope_account",
                    "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
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
            name: "transaction_labels",
            columns: table => new
            {
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                label_id = table.Column<Guid>(type: "uuid", nullable: false)
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

        migrationBuilder.CreateIndex(
            name: "ux_labels_household_name",
            table: "labels",
            columns: new[] { "household_id", "normalized_name" },
            unique: true,
            filter: "scope = 'Household'");

        migrationBuilder.CreateIndex(
            name: "ux_labels_account_name",
            table: "labels",
            columns: new[] { "household_id", "account_id", "normalized_name" },
            unique: true,
            filter: "scope = 'Account'");

        migrationBuilder.CreateIndex(
            name: "ix_labels_scope_account",
            table: "labels",
            columns: new[] { "household_id", "scope", "account_id" });

        migrationBuilder.CreateIndex(
            name: "IX_labels_household_id_account_id",
            table: "labels",
            columns: new[] { "household_id", "account_id" });

        migrationBuilder.CreateIndex(
            name: "ix_transaction_labels_label",
            table: "transaction_labels",
            columns: new[] { "household_id", "label_id" });

        migrationBuilder.CreateIndex(
            name: "IX_transaction_labels_household_id_transaction_id",
            table: "transaction_labels",
            columns: new[] { "household_id", "transaction_id" });

        migrationBuilder.Sql(
            """
            INSERT INTO labels
                (id, household_id, account_id, scope, name, normalized_name, created_at)
            SELECT
                min(id),
                household_id,
                account_id,
                scope,
                min(name),
                upper(trim(name)),
                TIMESTAMPTZ '2026-09-21 00:00:00+00'
            FROM transaction_labels_legacy
            GROUP BY household_id, account_id, scope, upper(trim(name));
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO transaction_labels (household_id, transaction_id, label_id)
            SELECT
                legacy.household_id,
                legacy.transaction_id,
                label.id
            FROM transaction_labels_legacy AS legacy
            JOIN labels AS label
              ON label.household_id = legacy.household_id
             AND label.scope = legacy.scope
             AND label.account_id IS NOT DISTINCT FROM legacy.account_id
             AND label.normalized_name = upper(trim(legacy.name))
            ON CONFLICT DO NOTHING;
            """);

        migrationBuilder.DropTable(name: "transaction_labels_legacy");

        migrationBuilder.DropPrimaryKey(
            name: "PK_transaction_aggregates",
            table: "transaction_aggregates");

        migrationBuilder.DropIndex(
            name: "ix_transaction_aggregates_household_period",
            table: "transaction_aggregates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_transaction_aggregates_period",
            table: "transaction_aggregates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_transaction_aggregates_non_negative",
            table: "transaction_aggregates");

        migrationBuilder.RenameColumn(
            name: "transaction_count",
            table: "transaction_aggregates",
            newName: "entry_count");

        migrationBuilder.AddColumn<string>(
            name: "dimension_type",
            table: "transaction_aggregates",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "Account");

        migrationBuilder.AddColumn<Guid>(
            name: "dimension_id",
            table: "transaction_aggregates",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE transaction_aggregates
            SET dimension_id = account_id
            WHERE dimension_id IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "dimension_id",
            table: "transaction_aggregates",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_transaction_aggregates",
            table: "transaction_aggregates",
            columns: new[]
            {
                "household_id", "account_id", "dimension_type", "dimension_id",
                "currency_code", "period", "period_start"
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_transaction_aggregates_dimension",
            table: "transaction_aggregates",
            sql: "dimension_type IN ('Account', 'Category')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_transaction_aggregates_period",
            table: "transaction_aggregates",
            sql: "period IN ('Day', 'Month', 'Year')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_transaction_aggregates_non_negative",
            table: "transaction_aggregates",
            sql: "income_amount >= 0 AND expense_amount >= 0 AND entry_count >= 0");

        migrationBuilder.CreateIndex(
            name: "ix_transaction_aggregates_household_dimension_period",
            table: "transaction_aggregates",
            columns: new[] { "household_id", "dimension_type", "period", "period_start", "currency_code" });

        migrationBuilder.CreateIndex(
            name: "ix_transaction_aggregates_dimension",
            table: "transaction_aggregates",
            columns: new[] { "household_id", "dimension_type", "dimension_id" });

        migrationBuilder.Sql(
            """
            INSERT INTO transaction_aggregates
                (household_id, account_id, dimension_type, dimension_id,
                 currency_code, period, period_start,
                 income_amount, expense_amount, entry_count, updated_at)
            SELECT
                transaction.household_id,
                transaction.account_id,
                'Account',
                transaction.account_id,
                transaction.currency_code,
                'Year',
                make_date(extract(year from transaction.occurred_on)::int, 1, 1),
                sum(CASE WHEN transaction.kind = 'Income' THEN transaction.amount ELSE 0 END),
                sum(CASE WHEN transaction.kind = 'Expense' THEN transaction.amount ELSE 0 END),
                count(*)::int,
                now()
            FROM transactions AS transaction
            WHERE transaction.status = 'Posted'
              AND transaction.visibility = 'Household'
            GROUP BY
                transaction.household_id,
                transaction.account_id,
                transaction.currency_code,
                make_date(extract(year from transaction.occurred_on)::int, 1, 1)
            ON CONFLICT DO NOTHING;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO transaction_aggregates
                (household_id, account_id, dimension_type, dimension_id,
                 currency_code, period, period_start,
                 income_amount, expense_amount, entry_count, updated_at)
            SELECT
                transaction.household_id,
                transaction.account_id,
                'Category',
                item.category_id,
                transaction.currency_code,
                bucket.period,
                bucket.period_start,
                sum(CASE WHEN transaction.kind = 'Income' THEN item.amount ELSE 0 END),
                sum(CASE WHEN transaction.kind = 'Expense' THEN item.amount ELSE 0 END),
                count(*)::int,
                now()
            FROM transaction_items AS item
            JOIN transactions AS transaction
              ON transaction.household_id = item.household_id
             AND transaction.id = item.transaction_id
            CROSS JOIN LATERAL (
                VALUES
                    ('Day'::text, transaction.occurred_on),
                    ('Month'::text, make_date(extract(year from transaction.occurred_on)::int, extract(month from transaction.occurred_on)::int, 1)),
                    ('Year'::text, make_date(extract(year from transaction.occurred_on)::int, 1, 1))
            ) AS bucket(period, period_start)
            WHERE transaction.status = 'Posted'
              AND transaction.visibility = 'Household'
            GROUP BY
                transaction.household_id,
                transaction.account_id,
                item.category_id,
                transaction.currency_code,
                bucket.period,
                bucket.period_start
            ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM transaction_aggregates
            WHERE dimension_type = 'Category'
               OR period = 'Year';
            """);

        migrationBuilder.DropPrimaryKey(
            name: "PK_transaction_aggregates",
            table: "transaction_aggregates");

        migrationBuilder.DropIndex(
            name: "ix_transaction_aggregates_household_dimension_period",
            table: "transaction_aggregates");

        migrationBuilder.DropIndex(
            name: "ix_transaction_aggregates_dimension",
            table: "transaction_aggregates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_transaction_aggregates_dimension",
            table: "transaction_aggregates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_transaction_aggregates_period",
            table: "transaction_aggregates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_transaction_aggregates_non_negative",
            table: "transaction_aggregates");

        migrationBuilder.DropColumn(
            name: "dimension_type",
            table: "transaction_aggregates");

        migrationBuilder.DropColumn(
            name: "dimension_id",
            table: "transaction_aggregates");

        migrationBuilder.RenameColumn(
            name: "entry_count",
            table: "transaction_aggregates",
            newName: "transaction_count");

        migrationBuilder.AddPrimaryKey(
            name: "PK_transaction_aggregates",
            table: "transaction_aggregates",
            columns: new[] { "household_id", "account_id", "currency_code", "period", "period_start" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_transaction_aggregates_period",
            table: "transaction_aggregates",
            sql: "period IN ('Day', 'Month')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_transaction_aggregates_non_negative",
            table: "transaction_aggregates",
            sql: "income_amount >= 0 AND expense_amount >= 0 AND transaction_count >= 0");

        migrationBuilder.CreateIndex(
            name: "ix_transaction_aggregates_household_period",
            table: "transaction_aggregates",
            columns: new[] { "household_id", "period", "period_start", "currency_code" });

        migrationBuilder.RenameTable(
            name: "transaction_labels",
            newName: "transaction_labels_join");

        migrationBuilder.CreateTable(
            name: "transaction_labels",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: true),
                scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transaction_labels", x => x.id);
                table.CheckConstraint("ck_transaction_labels_scope", "scope IN ('Household', 'Account')");
                table.CheckConstraint(
                    "ck_transaction_labels_scope_account",
                    "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
            });

        migrationBuilder.Sql(
            """
            INSERT INTO transaction_labels
                (id, household_id, transaction_id, account_id, scope, name)
            SELECT
                md5(link.transaction_id::text || ':' || link.label_id::text)::uuid,
                link.household_id,
                link.transaction_id,
                label.account_id,
                label.scope,
                label.name
            FROM transaction_labels_join AS link
            JOIN labels AS label
              ON label.household_id = link.household_id
             AND label.id = link.label_id;
            """);

        migrationBuilder.DropTable(name: "transaction_labels_join");
        migrationBuilder.DropTable(name: "labels");

        migrationBuilder.DropForeignKey(
            name: "fk_transaction_items_categories",
            table: "transaction_items");

        migrationBuilder.DropIndex(
            name: "ix_transaction_items_category",
            table: "transaction_items");

        migrationBuilder.DropColumn(
            name: "category_id",
            table: "transaction_items");

        migrationBuilder.DropColumn(
            name: "category_icon",
            table: "transaction_items");

        migrationBuilder.AlterColumn<string>(
            name: "category_name",
            table: "transaction_items",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100);

        migrationBuilder.DropTable(name: "categories");
        migrationBuilder.DropTable(name: "icons");
    }
}
