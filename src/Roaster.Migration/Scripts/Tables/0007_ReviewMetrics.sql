CREATE TABLE "ReviewMetrics" (
    "Id"               bigserial    PRIMARY KEY,
    "ReviewId"         bigint       NOT NULL REFERENCES "Reviews"("Id") ON DELETE CASCADE,
    "ModelId"          varchar(100),
    "QueueWaitMs"      int          NOT NULL DEFAULT 0,
    "ReviewDurationMs" int          NOT NULL DEFAULT 0,
    "TotalDurationMs"  int          NOT NULL DEFAULT 0,
    "CommentCount"     int          NOT NULL DEFAULT 0,
    "CodeLengthChars"  int          NOT NULL DEFAULT 0,
    "CreatedAt"        timestamptz  NOT NULL DEFAULT now()
);
