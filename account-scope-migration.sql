START TRANSACTION;
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM accounts a
        LEFT JOIN households h ON h.id = a.household_id
        WHERE h.id IS NULL
    ) THEN
        RAISE EXCEPTION 'Accounts reference a missing household. Resolve before migrating.';
    END IF;
    IF EXISTS (
        SELECT 1 FROM accounts
        WHERE ownership NOT IN ('Household', 'Personal')
           OR visibility NOT IN ('Household', 'Private')
           OR type NOT IN ('Checking', 'Savings', 'Cash', 'CreditCard')
           OR status NOT IN ('Active', 'Archived')
           OR (ownership = 'Personal' AND owner_household_member_id IS NULL)
    ) THEN
        RAISE EXCEPTION 'Invalid legacy account values. Resolve before migrating.';
    END IF;
END $$;

ALTER TABLE accounts DROP CONSTRAINT ck_accounts_ownership_visibility;

ALTER TABLE accounts RENAME COLUMN ownership TO scope;

ALTER TABLE accounts ADD CONSTRAINT ak_accounts_household_id_id UNIQUE (household_id, id);

ALTER TABLE accounts ADD CONSTRAINT fk_accounts_households_household_id FOREIGN KEY (household_id) REFERENCES households (id) ON DELETE RESTRICT;

CREATE TABLE account_owners (
    account_id uuid NOT NULL,
    household_member_id uuid NOT NULL,
    household_id uuid NOT NULL,
    CONSTRAINT pk_account_owners PRIMARY KEY (account_id, household_member_id),
    CONSTRAINT fk_account_owners_accounts FOREIGN KEY (household_id, account_id) REFERENCES accounts (household_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_account_owners_members FOREIGN KEY (household_id, household_member_id) REFERENCES household_members (household_id, id) ON DELETE RESTRICT
);

CREATE INDEX ix_account_owners_household_account ON account_owners (household_id, account_id);

CREATE INDEX ix_account_owners_household_member ON account_owners (household_id, household_member_id);

INSERT INTO account_owners (household_id, account_id, household_member_id)
SELECT household_id, id, owner_household_member_id
FROM accounts
WHERE owner_household_member_id IS NOT NULL;

ALTER TABLE accounts DROP CONSTRAINT "FK_accounts_household_members_household_id_owner_household_mem~";

DROP INDEX "IX_accounts_household_id_owner_household_member_id";

DROP INDEX "IX_accounts_owner_household_member_id";

ALTER TABLE accounts DROP COLUMN owner_household_member_id;

ALTER TABLE accounts ADD CONSTRAINT ck_accounts_scope CHECK (scope IN ('Household', 'Personal'));

ALTER TABLE accounts ADD CONSTRAINT ck_accounts_visibility CHECK (visibility IN ('Household', 'Private'));

ALTER TABLE accounts ADD CONSTRAINT ck_accounts_type CHECK (type IN ('Checking', 'Savings', 'Cash', 'CreditCard'));

ALTER TABLE accounts ADD CONSTRAINT ck_accounts_status CHECK (status IN ('Active', 'Archived'));

ALTER TABLE accounts ADD CONSTRAINT ck_accounts_scope_visibility CHECK ((scope = 'Household' AND visibility = 'Household' AND include_in_household_reports)
OR
(scope = 'Personal' AND (visibility <> 'Private' OR NOT include_in_household_reports)));

CREATE FUNCTION enforce_personal_account_holders()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    old_account uuid;
    new_account uuid;
    checked_account uuid;
    checked_scope text;
BEGIN
    IF TG_TABLE_NAME = 'accounts' THEN
        IF TG_OP <> 'INSERT' THEN old_account := OLD.id; END IF;
        IF TG_OP <> 'DELETE' THEN new_account := NEW.id; END IF;
    ELSE
        IF TG_OP <> 'INSERT' THEN old_account := OLD.account_id; END IF;
        IF TG_OP <> 'DELETE' THEN new_account := NEW.account_id; END IF;
    END IF;

    FOR checked_account IN
        SELECT DISTINCT value
        FROM unnest(ARRAY[old_account, new_account]) AS ids(value)
        WHERE value IS NOT NULL
        ORDER BY value
    LOOP
        -- Serialize checks against changes to the same parent.
        SELECT scope INTO checked_scope
        FROM accounts WHERE id = checked_account FOR UPDATE;

        -- A deleted account needs no holders.
        IF FOUND AND checked_scope = 'Personal' THEN
            IF NOT EXISTS (
                SELECT 1 FROM account_owners
                WHERE account_id = checked_account
            ) THEN
                RAISE EXCEPTION 'Personal accounts require at least one holder.'
                    USING ERRCODE = '23514',
                          CONSTRAINT = 'ck_personal_account_has_holder';
            END IF;
        END IF;
    END LOOP;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER ck_personal_account_holders_parent
AFTER INSERT OR UPDATE ON accounts
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION enforce_personal_account_holders();

CREATE CONSTRAINT TRIGGER ck_personal_account_holders_children
AFTER INSERT OR UPDATE OR DELETE ON account_owners
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION enforce_personal_account_holders();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260913062907_SeparateAccountScopeAndOwners', '10.0.11');

COMMIT;

