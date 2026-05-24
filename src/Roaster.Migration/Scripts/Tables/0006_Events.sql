CREATE TABLE "Events" (
    "Id"            bigserial    PRIMARY KEY,
    "SubmissionId"  bigint       NOT NULL REFERENCES "Submissions"("Id") ON DELETE CASCADE,
    "EventType"     smallint     NOT NULL,
    "CorrelationId" uuid         NOT NULL,
    "Metadata"      jsonb,
    "OccurredAt"    timestamptz  NOT NULL DEFAULT now()
);
