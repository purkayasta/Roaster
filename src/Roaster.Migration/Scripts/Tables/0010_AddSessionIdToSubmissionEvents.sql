ALTER TABLE "SubmissionEvents"
    ADD COLUMN IF NOT EXISTS "SessionId" uuid NULL;
