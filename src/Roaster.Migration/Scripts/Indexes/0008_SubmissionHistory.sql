CREATE INDEX idx_submission_history_submission_id ON "SubmissionHistory"("SubmissionId");
CREATE INDEX idx_submission_history_session_id   ON "SubmissionHistory"("SessionId");
CREATE INDEX idx_submission_history_changed_at   ON "SubmissionHistory"("ChangedAt" DESC);
