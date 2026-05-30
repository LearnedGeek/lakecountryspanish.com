-- One-off data update for SubscriptionTier rows seeded before the pricing change.
-- Apply manually on stg + prod after the redesign deploys.
-- New per-class rates: Starter $28, Standard $25, Intensive $23.
--
-- Run via:
--   psql $DATABASE_URL -f Migrations/Scripts/UpdateSubscriptionTierPricing.sql

BEGIN;

UPDATE "SubscriptionTiers" SET "MonthlyPrice" = 112.00 WHERE "Name" = 'Starter'   AND "ClassesPerMonth" = 4;
UPDATE "SubscriptionTiers" SET "MonthlyPrice" = 200.00 WHERE "Name" = 'Standard'  AND "ClassesPerMonth" = 8;
UPDATE "SubscriptionTiers" SET "MonthlyPrice" = 276.00 WHERE "Name" = 'Intensive' AND "ClassesPerMonth" = 12;

-- Verify before commit:
SELECT "Name", "ClassesPerMonth", "MonthlyPrice",
       ROUND("MonthlyPrice" / "ClassesPerMonth", 2) AS per_class
FROM "SubscriptionTiers"
ORDER BY "DisplayOrder";

COMMIT;
