BEGIN;

DO $$
DECLARE
    keep_id uuid := '965505ed-659c-4f29-ad5f-276db8a7de37';
BEGIN
    IF (SELECT COUNT(*) FROM "Stokvels" WHERE "Id" = keep_id AND "Name" = 'BitCube') <> 1 THEN
        RAISE EXCEPTION 'Expected BitCube not found. Cleanup cancelled.';
    END IF;

    IF (SELECT COUNT(*) FROM "Stokvels") <> 798 THEN
        RAISE EXCEPTION 'Stokvel count changed. Cleanup cancelled.';
    END IF;
END $$;

CREATE TEMP TABLE stokvels_to_remove ON COMMIT DROP AS
SELECT "Id"
FROM "Stokvels"
WHERE "Id" <> '965505ed-659c-4f29-ad5f-276db8a7de37';

SELECT COUNT(*) AS "StokvelsToRemove"
FROM stokvels_to_remove;

DELETE FROM "ContributionArrears"
WHERE "StokvelId" IN (SELECT "Id" FROM stokvels_to_remove);

DELETE FROM "Contributions"
WHERE "StokvelId" IN (SELECT "Id" FROM stokvels_to_remove);

DELETE FROM "Payouts"
WHERE "StokvelId" IN (SELECT "Id" FROM stokvels_to_remove);

DELETE FROM "Stokvels"
WHERE "Id" IN (SELECT "Id" FROM stokvels_to_remove);

DO $$
BEGIN
    IF (SELECT COUNT(*) FROM "Stokvels") <> 1
       OR NOT EXISTS (
           SELECT 1 FROM "Stokvels"
           WHERE "Id" = '965505ed-659c-4f29-ad5f-276db8a7de37'
       ) THEN
        RAISE EXCEPTION 'Final verification failed. Cleanup cancelled.';
    END IF;
END $$;

COMMIT;

SELECT "Id", "Name"
FROM "Stokvels";
