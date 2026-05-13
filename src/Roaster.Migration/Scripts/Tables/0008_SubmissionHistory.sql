CREATE TABLE "SubmissionHistory" (
    "Id"           bigserial    PRIMARY KEY,
    "SubmissionId" bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "SessionId"    uuid         NOT NULL REFERENCES "Sessions"("Id") ON DELETE CASCADE,
    "Payload"      jsonb        NOT NULL,
    "ChangedAt"    timestamptz  NOT NULL DEFAULT now()
);
