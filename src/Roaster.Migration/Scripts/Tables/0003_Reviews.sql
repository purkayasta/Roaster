CREATE TABLE "Reviews" (
    "Id"           bigserial    PRIMARY KEY,
    "SubmissionId" bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "Summary"      text,
    "Comments"     jsonb        NOT NULL DEFAULT '[]',
    "CreatedAt"    timestamptz  NOT NULL DEFAULT now()
);
