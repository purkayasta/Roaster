CREATE INDEX idx_thread_replies_thread ON "ThreadReplies"("ThreadId");
CREATE INDEX idx_thread_replies_time   ON "ThreadReplies"("CreatedAt");
