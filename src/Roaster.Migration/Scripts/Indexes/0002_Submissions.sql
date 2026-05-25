CREATE INDEX idx_submissions_status     ON "Submissions"("Status");
CREATE INDEX idx_submissions_created_at ON "Submissions"("CreatedAt" DESC);
CREATE INDEX idx_submissions_session_id ON "Submissions"("SessionId");
