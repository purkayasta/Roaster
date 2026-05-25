CREATE TABLE "ThreadReplies" (
    "Id"          bigserial    PRIMARY KEY,
    "ThreadId"    bigint       NOT NULL REFERENCES "CommentThreads"("Id") ON DELETE CASCADE,
    "Content"     text,
    "IsAiReply"   boolean      NOT NULL DEFAULT false,
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now()
);
