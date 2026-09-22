using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedIconsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH seeded_categories AS (
                    SELECT (
                        '00000000-0000-4000-8000-000000000'
                        || lpad(value::text, 3, '0')
                    )::uuid AS id
                    FROM generate_series(1, 100) AS seed(value)
                )
                DELETE FROM categories
                WHERE id IN (SELECT id FROM seeded_categories)
                  AND parent_id IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                WITH seeded_categories AS (
                    SELECT (
                        '00000000-0000-4000-8000-000000000'
                        || lpad(value::text, 3, '0')
                    )::uuid AS id
                    FROM generate_series(1, 100) AS seed(value)
                )
                DELETE FROM categories
                WHERE id IN (SELECT id FROM seeded_categories);
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM icons
                WHERE id BETWEEN 1 AND 240
                  AND name = 'icon-' || lpad(id::text, 3, '0');
                """);
        }
    }
}
