CREATE TABLE "Submissions" (
    "Id"         bigserial    PRIMARY KEY,
    "Payload"    jsonb        NOT NULL,
    "Status"     smallint     NOT NULL DEFAULT 10,
    "CreatedAt"  timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"  timestamptz  NOT NULL DEFAULT now()
);
