CREATE TABLE "Submissions" (
    "Id"        bigserial    PRIMARY KEY,
    "SessionId" uuid         NOT NULL REFERENCES "Sessions"("Id") ON DELETE CASCADE,
    "Payloads"  jsonb        NOT NULL DEFAULT '[]',
    "Status"    smallint     NOT NULL DEFAULT 10,
    "CreatedAt" timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz  NOT NULL DEFAULT now()
);
