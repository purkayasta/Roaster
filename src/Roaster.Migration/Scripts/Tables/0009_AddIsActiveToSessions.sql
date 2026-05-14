ALTER TABLE "Sessions"
    ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT true;
