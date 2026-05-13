CREATE TABLE "CommentThreads" (
    "Id"          bigserial    PRIMARY KEY,
    "ReviewId"    bigint       NOT NULL REFERENCES "Reviews"("Id") ON DELETE CASCADE,
    "LineNumber"  int          NOT NULL,
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now(),
    UNIQUE ("ReviewId", "LineNumber")
);
